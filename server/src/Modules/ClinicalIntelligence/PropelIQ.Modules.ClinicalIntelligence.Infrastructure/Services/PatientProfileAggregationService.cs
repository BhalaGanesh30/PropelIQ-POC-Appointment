using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Aggregates clinical facts from all categories in parallel for the 360° patient profile.
///
/// Design decisions:
/// - Each category is queried independently so one failure does not block others (Edge Case 1).
/// - <see cref="Task.WhenAll"/> runs all category queries concurrently to minimise latency (NFR-002).
/// - Results are cached in Redis with a 60-second TTL via <see cref="ICacheService"/> (TR-004).
/// - Cache misses fall back to EF Core without surfacing Redis errors (ICacheService contract).
/// - OpenTelemetry spans track aggregation duration with patient.id and result tags.
/// </summary>
public sealed class PatientProfileAggregationService : IPatientProfileAggregationService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.ClinicalIntelligence.PatientProfileAggregationService");

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IClinicalFactRepository _factRepository;
    private readonly ICacheService _cache;
    private readonly ILogger<PatientProfileAggregationService> _logger;

    /// <summary>All fact categories queried in parallel per profile request.</summary>
    private static readonly string[] Categories = ["medication", "allergy", "diagnosis", "finding"];

    public PatientProfileAggregationService(
        IClinicalFactRepository factRepository,
        ICacheService cache,
        ILogger<PatientProfileAggregationService> logger)
    {
        _factRepository = factRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PatientProfileDto> AggregateProfileAsync(
        Guid patientId,
        ProfileQuery query,
        CancellationToken ct = default)
    {
        var cacheKey = $"profile:{patientId}:limit:{query.Limit}:offset:{query.Offset}";

        // ── Cache read ────────────────────────────────────────────────────────
        var cached = await _cache.GetAsync<PatientProfileDto>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Profile cache hit for patient {PatientId}", patientId);
            return cached;
        }

        // ── Aggregation ───────────────────────────────────────────────────────
        using var activity = ActivitySource.StartActivity(
            "PatientProfileAggregationService.AggregateAsync",
            ActivityKind.Internal);

        activity?.SetTag("patient.id", patientId);
        activity?.SetTag("query.tab", query.Tab);
        activity?.SetTag("query.limit", query.Limit);
        activity?.SetTag("query.offset", query.Offset);

        var sw = Stopwatch.StartNew();

        // Fan-out: run all category queries in parallel (NFR-002 minimum latency).
        var categoryTasks = Categories
            .Select(cat => QueryCategoryAsync(patientId, cat, query.Limit, query.Offset, ct))
            .ToArray();

        var results = await Task.WhenAll(categoryTasks);
        sw.Stop();

        // Collect successes and failures.
        var medications = new List<ClinicalFactDto>();
        var allergies = new List<ClinicalFactDto>();
        var diagnoses = new List<ClinicalFactDto>();
        var findings = new List<ClinicalFactDto>();
        var partialSources = new List<PartialSourceDto>();

        for (var i = 0; i < Categories.Length; i++)
        {
            var (category, facts, errorReason) = results[i];
            if (errorReason is not null)
            {
                partialSources.Add(new PartialSourceDto
                {
                    Category = category,
                    ErrorReason = errorReason,
                });
                continue;
            }

            switch (category)
            {
                case "medication": medications.AddRange(facts); break;
                case "allergy":    allergies.AddRange(facts);   break;
                case "diagnosis":  diagnoses.AddRange(facts);   break;
                case "finding":    findings.AddRange(facts);    break;
            }
        }

        // Timeline: merge all successfully-loaded facts ordered by clinical date desc.
        var allFacts = medications
            .Concat(allergies)
            .Concat(diagnoses)
            .Concat(findings)
            .OrderByDescending(f => f.FactDate ?? DateTimeOffset.MinValue)
            .ToList();

        var totalCount = medications.Count + allergies.Count + diagnoses.Count + findings.Count;

        var profile = new PatientProfileDto
        {
            PatientId      = patientId,
            Medications    = medications,
            Allergies      = allergies,
            Diagnoses      = diagnoses,
            Findings       = findings,
            Timeline       = allFacts,
            Partial        = partialSources.Count > 0,
            PartialSources = partialSources,
            TotalCount     = totalCount,
        };

        activity?.SetTag("result.total_facts", totalCount);
        activity?.SetTag("result.partial", profile.Partial);

        _logger.LogInformation(
            "Profile aggregated for patient {PatientId}: {TotalFacts} facts in {ElapsedMs}ms (partial={Partial})",
            patientId, totalCount, sw.ElapsedMilliseconds, profile.Partial);

        // ── Cache write ───────────────────────────────────────────────────────
        await _cache.SetAsync(cacheKey, profile, CacheTtl, ct);

        return profile;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Queries a single fact category. Returns an error reason string on failure instead of
    /// propagating — this enables partial success (Edge Case 1).
    /// </summary>
    private async Task<(string Category, List<ClinicalFactDto> Facts, string? ErrorReason)>
        QueryCategoryAsync(
            Guid patientId,
            string category,
            int limit,
            int offset,
            CancellationToken ct)
    {
        try
        {
            var (rawFacts, _) = await _factRepository.GetByPatientIdGroupedAsync(
                patientId, category, limit, offset, ct);

            var dtos = rawFacts.Select(MapToDto).ToList();
            return (category, dtos, null);
        }
        catch (Exception ex)
        {
            // Log full exception internally but surface only a sanitised message to the client.
            _logger.LogError(ex,
                "Failed to load {Category} facts for patient {PatientId}", category, patientId);

            return (category, [], "Data temporarily unavailable. Please try again shortly.");
        }
    }

    private static ClinicalFactDto MapToDto(ClinicalFact fact)
    {
        var doc = fact.Document;
        SourceDocumentDto? sourceDoc = null;

        if (doc is not null)
        {
            sourceDoc = new SourceDocumentDto
            {
                DocumentId  = doc.Id,
                DisplayName = doc.DisplayName ?? doc.FileName,
                UploadedAt  = doc.CreatedAt,
            };
        }

        return new ClinicalFactDto
        {
            FactId          = fact.Id,
            FactType        = fact.FactType,
            Name            = fact.Name,
            Value           = fact.Value,
            ConfidenceScore = fact.ConfidenceScore,
            NeedsReview     = fact.NeedsReview,
            Verified        = fact.Verified,
            FactDate        = fact.FactDate,
            SourceDocument  = sourceDoc,
        };
    }
}

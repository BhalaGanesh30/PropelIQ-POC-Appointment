using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Aggregates clinical timeline events from <c>clinical_facts</c> and <c>clinical_documents</c>
/// in parallel, applies server-side category and date-range filters, and caches results in
/// Redis with a 60-second TTL (US_048, FR-CA-005, TR-004, NFR-002).
///
/// Source routing strategy:
///   - <c>category == null || "All"</c> → parallel fan-out to both facts and documents.
///   - <c>category == "Documents"</c>   → documents source only.
///   - Any other category               → facts source only (mapped to factType string).
///
/// OpenTelemetry instrumentation: activity span + <c>timeline.query.duration_ms</c> histogram.
/// </summary>
public sealed class TimelineService : ITimelineService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.ClinicalIntelligence.TimelineService");

    // Histogram metric tracks query latency for NFR-002 alerting (p95 < 500 ms).
    private static readonly Meter Meter =
        new("PropelIQ.ClinicalIntelligence");

    private static readonly Histogram<double> QueryDuration =
        Meter.CreateHistogram<double>(
            "timeline.query.duration_ms",
            unit: "ms",
            description: "Duration of clinical timeline queries in milliseconds.");

    private readonly IClinicalFactRepository _factRepository;
    private readonly IClinicalDocumentRepository _documentRepository;
    private readonly ITimelineCacheService _cache;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(
        IClinicalFactRepository factRepository,
        IClinicalDocumentRepository documentRepository,
        ITimelineCacheService cache,
        ILogger<TimelineService> logger)
    {
        _factRepository     = factRepository;
        _documentRepository = documentRepository;
        _cache              = cache;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<TimelineResponseDto> GetTimelineAsync(
        Guid patientId,
        TimelineQuery query,
        CancellationToken ct = default)
    {
        // ── Cache read ────────────────────────────────────────────────────────
        var cached = await _cache.GetAsync(patientId, query, ct);
        if (cached is not null)
        {
            _logger.LogDebug(
                "Timeline cache HIT for patient {PatientId} category={Category}",
                patientId, query.Category ?? "All");
            return cached;
        }

        // ── OTel span ─────────────────────────────────────────────────────────
        using var activity = ActivitySource.StartActivity(
            "TimelineService.GetTimelineAsync",
            ActivityKind.Internal);

        activity?.SetTag("patient.id", patientId);
        activity?.SetTag("query.category", query.Category ?? "All");
        activity?.SetTag("query.date_from", query.DateFrom?.ToString("O"));
        activity?.SetTag("query.date_to", query.DateTo?.ToString("O"));

        var sw = Stopwatch.StartNew();

        // ── Source routing ────────────────────────────────────────────────────
        var normalizedCategory = query.Category?.Trim();
        var isAll = string.IsNullOrWhiteSpace(normalizedCategory)
                    || normalizedCategory.Equals("All", StringComparison.OrdinalIgnoreCase);
        var isDocumentsOnly = normalizedCategory?.Equals("Documents", StringComparison.OrdinalIgnoreCase) == true;

        List<TimelineEventDto> merged;

        if (isAll)
        {
            // Parallel fan-out: both sources run concurrently (NFR-002).
            var (facts, documents) = await FetchBothAsync(patientId, null, query.DateFrom, query.DateTo, ct);
            merged = [..facts, ..documents];
        }
        else if (isDocumentsOnly)
        {
            merged = await _documentRepository.GetTimelineDocumentsAsync(
                patientId, query.DateFrom, query.DateTo, ct);
        }
        else
        {
            // Map display category back to the stored factType value.
            var factType = MapCategoryToFactType(normalizedCategory!);
            merged = await _factRepository.GetTimelineFactsAsync(
                patientId, factType, query.DateFrom, query.DateTo, ct);
        }

        // ── Sort reverse-chronological (AC-1) ─────────────────────────────────
        merged.Sort(static (a, b) => b.EventDate.CompareTo(a.EventDate));

        sw.Stop();

        // ── OTel metrics ──────────────────────────────────────────────────────
        QueryDuration.Record(sw.Elapsed.TotalMilliseconds);
        activity?.SetTag("result.total_events", merged.Count);

        _logger.LogDebug(
            "Timeline query for patient {PatientId}: {Count} events in {Ms}ms",
            patientId, merged.Count, sw.ElapsedMilliseconds);

        var response = new TimelineResponseDto
        {
            Events     = merged,
            TotalCount = merged.Count,
        };

        // ── Cache write ───────────────────────────────────────────────────────
        await _cache.SetAsync(patientId, query, response, ct);

        return response;
    }

    /// <summary>
    /// Fetches facts and documents in parallel when no category filter restricts the source (AC-1).
    /// </summary>
    private async Task<(List<TimelineEventDto> Facts, List<TimelineEventDto> Documents)> FetchBothAsync(
        Guid patientId,
        string? factType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct)
    {
        var factsTask = _factRepository.GetTimelineFactsAsync(patientId, factType, from, to, ct);
        var docsTask  = _documentRepository.GetTimelineDocumentsAsync(patientId, from, to, ct);

        await Task.WhenAll(factsTask, docsTask);

        return (factsTask.Result, docsTask.Result);
    }

    /// <summary>
    /// Maps the UI display category name to the stored <c>fact_type</c> column value.
    /// Unknown categories fall through to the raw value (caller already filtered to known categories).
    /// </summary>
    private static string MapCategoryToFactType(string category) =>
        category.ToLowerInvariant() switch
        {
            "medications" => "medication",
            "allergies"   => "allergy",
            "diagnoses"   => "diagnosis",
            "findings"    => "finding",
            _             => category.ToLowerInvariant(),
        };
}

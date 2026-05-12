using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Options;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Full ICD-10 coding suggestion pipeline orchestrator (US_049, AC-1 through AC-5).
///
/// Pipeline order:
///   1. Cache check  — return cached result if present (NFR-003 ≤ 5 s).
///   2. Preflight    — 422 if no clinical facts exist (Edge Case 2).
///   3. Retrieval    — ACL-filtered top-10 evidence chunks via HNSW cosine distance (AIR-010).
///   4. LLM call     — GPT-4.1 structured output via LiteLLM proxy.
///   5. Validation   — schema + confidence threshold check.
///   6. Persist      — insert <c>coding_decisions</c> rows in Pending state (AC-1).
///   7. Cache write  — TTL 300 s (NFR-003).
///   8. Return       — <see cref="CodingSuggestionResponseDto"/> with ranked suggestions.
///
/// Fallback path (AIR-005): when circuit breaker is open or LLM returns null,
/// the empty response (<c>LowConfidence=true, InsufficientEvidence=true</c>) is returned
/// rather than propagating an unhandled exception.
/// </summary>
internal sealed class CodingSuggestionOrchestrator : ICodingSuggestionOrchestrator
{
    private const string QueryText = "diagnosis icd10 clinical conditions chronic acute";
    private const int TopKEvidence = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(300);

    private readonly IClinicalFactRepository _factRepo;
    private readonly ICodingDecisionRepository _decisionRepo;
    private readonly IEvidenceRetrievalService _retrieval;
    private readonly ICodingAiGatewayClient _aiClient;
    private readonly ICodingSchemaValidator _validator;
    private readonly ICacheService _cache;
    private readonly IOptions<CodingSuggestionOptions> _options;
    private readonly ILogger<CodingSuggestionOrchestrator> _logger;

    public CodingSuggestionOrchestrator(
        IClinicalFactRepository factRepo,
        ICodingDecisionRepository decisionRepo,
        IEvidenceRetrievalService retrieval,
        ICodingAiGatewayClient aiClient,
        ICodingSchemaValidator validator,
        ICacheService cache,
        IOptions<CodingSuggestionOptions> options,
        ILogger<CodingSuggestionOrchestrator> logger)
    {
        _factRepo    = factRepo;
        _decisionRepo = decisionRepo;
        _retrieval   = retrieval;
        _aiClient    = aiClient;
        _validator   = validator;
        _cache       = cache;
        _options     = options;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task<CodingSuggestionResponseDto> GenerateSuggestionsAsync(
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default)
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity("coding_suggestion.generate");
        activity?.SetTag("patient.id", patientId);

        // Step 1: Cache check.
        var cacheKey = $"coding-suggestion:{patientId}";
        var cached   = await _cache.GetAsync<CodingSuggestionResponseDto>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for coding suggestions (patient {PatientId}).", patientId);
            return cached;
        }

        // Step 2: Preflight — 422 when no clinical facts are available.
        var hasFacts = await _factRepo.HasFactsAsync(patientId, ct);
        if (!hasFacts)
        {
            throw new InsufficientClinicalDataException();
        }

        // Step 3: Evidence retrieval.
        var evidence = await _retrieval.RetrieveAsync(patientId, QueryText, TopKEvidence, ct);
        if (evidence.Count == 0)
        {
            _logger.LogWarning("No embedded evidence found for patient {PatientId}. Returning empty result.", patientId);
            return BuildEmptyFallback();
        }

        // Step 4: LLM inference (with PII redaction + ACL filter — US_054).
        var rawJson = await _aiClient.RequestSuggestionsAsync(evidence, patientId, clinicianId, ct);
        if (rawJson is null)
        {
            _logger.LogWarning("AI gateway returned null for patient {PatientId}. Activating fallback.", patientId);
            return BuildEmptyFallback();
        }

        // Step 5: Schema validation.
        var llmResponse = _validator.ValidateAndParse(rawJson);
        if (llmResponse is null)
        {
            _logger.LogWarning("LLM schema validation failed for patient {PatientId}. Activating fallback.", patientId);
            return BuildEmptyFallback();
        }

        // Build lookup for evidence chunks by FactId.
        var evidenceByFactId = evidence.ToDictionary(e => e.FactId);

        // Map to domain CodingDecision entities + DTOs simultaneously.
        var decisions = new List<CodingDecision>(llmResponse.Suggestions.Count);
        var dtoItems  = new List<IcdSuggestionDto>(llmResponse.Suggestions.Count);

        foreach (var item in llmResponse.Suggestions.OrderByDescending(s => s.Confidence))
        {
            // Find first citation's document to satisfy the FK (US_049 design note).
            Guid documentId = item.FactIds
                .Select(fid => evidenceByFactId.TryGetValue(fid, out var c) ? c.DocumentId : (Guid?)null)
                .FirstOrDefault(d => d.HasValue)
                .GetValueOrDefault();

            // CodingDecision.Id is auto-generated by BaseEntity — capture it after construction.
            var decision = new CodingDecision
            {
                PatientId       = patientId,
                DocumentId      = documentId,
                CodeType        = "ICD-10",
                SuggestedCode   = item.Icd10Code!,
                Rationale       = item.Rationale,
                ConfidenceScore = item.Confidence,
                ReviewerAction  = ReviewerAction.Pending,
                FactId          = item.FactIds.FirstOrDefault() == Guid.Empty ? null : item.FactIds.FirstOrDefault(),
            };
            decisions.Add(decision);

            dtoItems.Add(new IcdSuggestionDto
            {
                DecisionId  = decision.Id,
                IcdCode     = item.Icd10Code!,
                Description = item.Description!,
                Confidence  = item.Confidence,
                Rationale   = item.Rationale ?? string.Empty,
                Citations   = item.FactIds
                    .Select(fid => evidenceByFactId.TryGetValue(fid, out var c) ? c : null)
                    .Where(c => c is not null)
                    .Select(c => new ClinicalFactCitationDto
                    {
                        FactId   = c!.FactId,
                        FactType = c.FactType,
                        Name     = c.Name,
                        Value    = c.Value,
                        FactDate = c.FactDate,
                    })
                    .ToList(),
            });
        }

        // Step 6: Persist coding_decisions in Pending state.
        try
        {
            await _decisionRepo.InsertPendingAsync(decisions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist coding decisions for patient {PatientId}.", patientId);
            // Non-fatal — still return the AI result to the caller.
        }

        // Step 7: Build response + confidence check.
        var threshold          = _options.Value.ConfidenceThreshold;
        var topConfidence      = dtoItems.FirstOrDefault()?.Confidence ?? 0m;
        var lowConfidence      = topConfidence < threshold;
        var insufficientEvidence = dtoItems.Count < 3;

        var result = new CodingSuggestionResponseDto
        {
            Suggestions         = dtoItems,
            LowConfidence       = lowConfidence,
            InsufficientEvidence = insufficientEvidence,
        };

        // Step 8: Cache write.
        await _cache.SetAsync(cacheKey, result, CacheTtl, ct);

        return result;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static CodingSuggestionResponseDto BuildEmptyFallback()
        => new()
        {
            Suggestions          = [],
            LowConfidence        = true,
            InsufficientEvidence = true,
        };
}

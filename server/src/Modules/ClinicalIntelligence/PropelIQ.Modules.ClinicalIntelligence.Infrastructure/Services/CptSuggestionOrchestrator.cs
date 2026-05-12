using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Options;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Full CPT/E/M suggestion Hybrid pipeline orchestrator (US_050, FR-MC-002).
///
/// Pipeline order:
///   1.  Cache check        — return cached result if present (90 s TTL).
///   2.  Appointment lookup — resolve appointment type for CPT candidacy check.
///   3.  Appointment mapper — short-circuit with <c>noSuggestionForAppointmentType: true</c>
///                            when the type is not CPT-mappable (Edge Case 1).
///   4.  Freshness check    — set <c>staleDatabaseWarning</c> flag; continue regardless (Edge Case 2).
///   5.  Evidence retrieval — ACL-filtered top-10 clinical facts via pgvector HNSW (AIR-010).
///   6.  LLM inference      — GPT-4.1 via LiteLLM with CPT-specific prompt (AIR-006).
///   7.  Schema validation  — CPT+E/M output schema; retry once on failure (AIR-008).
///   8.  CPT validation     — remove deprecated/non-existent codes (deterministic guardrail).
///   9.  Confidence check   — <c>lowConfidence: true</c> when min confidence &lt; threshold (AIR-005).
///   10. Citation attach    — resolve fact_ids → <see cref="ClinicalFactCitationDto"/> (AIR-004).
///   11. Persist            — insert <c>coding_decisions</c> with <c>cpt_code</c> set, Pending state.
///   12. Cache write        — TTL 90 s.
///   13. Return             — <see cref="CptSuggestionResponseDto"/>.
///
/// Fallback: circuit breaker open or all codes rejected → empty response, <c>LowConfidence = true</c>.
/// Never throws for domain-level edge cases — all surfaced via response flags.
/// </summary>
internal sealed class CptSuggestionOrchestrator : ICptSuggestionOrchestrator
{
    private const string CptQueryText = "procedure billing CPT appointment visit examination";
    private const int TopKEvidence = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(90);

    private readonly AppDbContext _db;
    private readonly IEvidenceRetrievalService _retrieval;
    private readonly ICptCodingAiGatewayClient _aiClient;
    private readonly ICodingSchemaValidator _validator;
    private readonly ICptCodeValidationService _cptValidator;
    private readonly ICptCodeFreshnessService _freshness;
    private readonly IAppointmentTypeMapper _appointmentMapper;
    private readonly ICodingDecisionRepository _decisionRepo;
    private readonly ICacheService _cache;
    private readonly IOptions<CodingSuggestionOptions> _options;
    private readonly ILogger<CptSuggestionOrchestrator> _logger;

    public CptSuggestionOrchestrator(
        AppDbContext db,
        IEvidenceRetrievalService retrieval,
        ICptCodingAiGatewayClient aiClient,
        ICodingSchemaValidator validator,
        ICptCodeValidationService cptValidator,
        ICptCodeFreshnessService freshness,
        IAppointmentTypeMapper appointmentMapper,
        ICodingDecisionRepository decisionRepo,
        ICacheService cache,
        IOptions<CodingSuggestionOptions> options,
        ILogger<CptSuggestionOrchestrator> logger)
    {
        _db                = db;
        _retrieval         = retrieval;
        _aiClient          = aiClient;
        _validator         = validator;
        _cptValidator      = cptValidator;
        _freshness         = freshness;
        _appointmentMapper = appointmentMapper;
        _decisionRepo      = decisionRepo;
        _cache             = cache;
        _options           = options;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<CptSuggestionResponseDto> GenerateCptSuggestionsAsync(
        Guid patientId,
        Guid appointmentId,
        CancellationToken ct = default)
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity("cpt_suggestion.generate");
        activity?.SetTag("patient.id", patientId);
        activity?.SetTag("appointment.id", appointmentId);

        var cacheKey = $"cpt-suggestion:{patientId}:{appointmentId}";

        // Step 1: Cache check.
        var cached = await _cache.GetAsync<CptSuggestionResponseDto>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug(
                "Cache hit for CPT suggestions (patient {PatientId}, appointment {AppointmentId}).",
                patientId, appointmentId);
            return cached;
        }

        // Step 2: Appointment lookup — resolve type.
        var appointment = await _db.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId, ct);

        if (appointment is null)
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} not found for patient {PatientId}. Returning no-suggestion.",
                appointmentId, patientId);
            return NoSuggestionForType();
        }

        var appointmentType = appointment.AppointmentType;

        // Step 3: Appointment type mapper — Edge Case 1.
        if (!_appointmentMapper.IsMappableToCpt(appointmentType))
        {
            _logger.LogInformation(
                "Appointment type '{AppointmentType}' is not CPT-mappable. Returning no-suggestion.",
                appointmentType);
            return NoSuggestionForType();
        }

        // Step 4: Freshness check — Edge Case 2.
        var freshness = await _freshness.CheckFreshnessAsync(ct);
        var staleWarning = freshness.IsStale;

        // Step 5: Evidence retrieval (AIR-010).
        var evidence = await _retrieval.RetrieveAsync(patientId, CptQueryText, TopKEvidence, ct);
        if (evidence.Count == 0)
        {
            _logger.LogWarning(
                "No embedded evidence found for patient {PatientId}. Returning empty CPT result.",
                patientId);
            return BuildEmptyFallback(staleWarning);
        }

        // Step 6: LLM inference.
        var rawJson = await _aiClient.RequestCptSuggestionsAsync(appointmentType, evidence, ct);
        if (rawJson is null)
        {
            _logger.LogWarning(
                "AI gateway returned null for CPT suggestions (patient {PatientId}). Activating fallback.",
                patientId);
            return BuildEmptyFallback(staleWarning);
        }

        // Step 7: Schema validation (AIR-008) — retry once on failure.
        var llmResponse = _validator.ValidateAndParseCpt(rawJson);
        if (llmResponse is null)
        {
            _logger.LogWarning(
                "CPT schema validation failed on first attempt; retrying LLM for patient {PatientId}.",
                patientId);
            rawJson = await _aiClient.RequestCptSuggestionsAsync(appointmentType, evidence, ct);
            if (rawJson is not null)
            {
                llmResponse = _validator.ValidateAndParseCpt(rawJson);
            }

            if (llmResponse is null)
            {
                _logger.LogWarning("CPT schema validation failed on retry. Activating fallback.");
                return BuildEmptyFallback(staleWarning);
            }
        }

        // Step 8: Deterministic CPT code validation — remove hallucinated / deprecated codes.
        var suggestedCodes = llmResponse.CptSuggestions
            .Select(s => s.CptCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .ToList();

        var activeCodes = await _cptValidator.FilterActiveAsync(suggestedCodes, ct);

        var validatedSuggestions = llmResponse.CptSuggestions
            .Where(s => s.CptCode is not null && activeCodes.Contains(s.CptCode!))
            .ToList();

        if (validatedSuggestions.Count == 0)
        {
            _logger.LogWarning(
                "All CPT codes rejected by deterministic validation for patient {PatientId}. Fallback.",
                patientId);
            return BuildEmptyFallback(staleWarning, lowConfidence: true);
        }

        // Step 9: Confidence threshold check (AIR-005).
        var threshold     = _options.Value.ConfidenceThreshold;
        var topConfidence = validatedSuggestions.OrderByDescending(s => s.Confidence).First().Confidence;
        var lowConfidence = topConfidence < threshold;

        // Build lookup for evidence chunks by FactId.
        var evidenceByFactId = evidence.ToDictionary(e => e.FactId);

        // Step 10: Citation attachment + build CPT decisions.
        var cptDecisions = new List<CodingDecision>(validatedSuggestions.Count);
        var cptDtos      = new List<CptSuggestionDto>(validatedSuggestions.Count);

        foreach (var item in validatedSuggestions.OrderByDescending(s => s.Confidence))
        {
            Guid documentId = item.FactIds
                .Select(fid => evidenceByFactId.TryGetValue(fid, out var c) ? c.DocumentId : (Guid?)null)
                .FirstOrDefault(d => d.HasValue)
                .GetValueOrDefault();

            var decision = new CodingDecision
            {
                PatientId       = patientId,
                DocumentId      = documentId,
                CodeType        = "CPT",
                SuggestedCode   = item.CptCode!,
                CptCode         = item.CptCode,
                Rationale       = item.Rationale,
                ConfidenceScore = item.Confidence,
                ReviewerAction  = ReviewerAction.Pending,
                FactId          = item.FactIds.FirstOrDefault() == Guid.Empty
                    ? null
                    : item.FactIds.FirstOrDefault(),
            };
            cptDecisions.Add(decision);

            cptDtos.Add(new CptSuggestionDto
            {
                DecisionId  = decision.Id,
                CptCode     = item.CptCode!,
                Description = item.Description ?? string.Empty,
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

        // Build E/M suggestion DTO.
        EmSuggestionDto? emDto = null;
        if (llmResponse.EmSuggestion is { EmLevel: not null } em)
        {
            // Persist E/M as a separate CodingDecision row with CodeType = "E/M".
            Guid emDocumentId = em.ComplexityFactors.Count == 0 ? Guid.Empty
                : evidenceByFactId.Values.Select(c => c.DocumentId).FirstOrDefault();

            var emDecision = new CodingDecision
            {
                PatientId       = patientId,
                DocumentId      = emDocumentId,
                CodeType        = "E/M",
                SuggestedCode   = em.EmLevel,
                Rationale       = em.Rationale,
                ConfidenceScore = em.Confidence,
                ReviewerAction  = ReviewerAction.Pending,
            };
            cptDecisions.Add(emDecision);

            emDto = new EmSuggestionDto
            {
                DecisionId        = emDecision.Id,
                EmLevel           = em.EmLevel,
                Description       = em.Description ?? string.Empty,
                Confidence        = em.Confidence,
                Rationale         = em.Rationale ?? string.Empty,
                ComplexityFactors = em.ComplexityFactors,
            };
        }

        // Step 11: Persist coding decisions (non-fatal on failure).
        try
        {
            await _decisionRepo.InsertPendingAsync(cptDecisions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist CPT coding decisions for patient {PatientId}.", patientId);
        }

        var result = new CptSuggestionResponseDto
        {
            CptSuggestions             = cptDtos,
            EmSuggestion               = emDto,
            LowConfidence              = lowConfidence,
            StaleDatabaseWarning       = staleWarning,
            NoSuggestionForAppointmentType = false,
        };

        // Step 12: Cache write (90 s TTL).
        await _cache.SetAsync(cacheKey, result, CacheTtl, ct);

        return result;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static CptSuggestionResponseDto NoSuggestionForType() =>
        new()
        {
            CptSuggestions             = [],
            EmSuggestion               = null,
            LowConfidence              = false,
            StaleDatabaseWarning       = false,
            NoSuggestionForAppointmentType = true,
        };

    private static CptSuggestionResponseDto BuildEmptyFallback(
        bool staleWarning,
        bool lowConfidence = false) =>
        new()
        {
            CptSuggestions             = [],
            EmSuggestion               = null,
            LowConfidence              = lowConfidence,
            StaleDatabaseWarning       = staleWarning,
            NoSuggestionForAppointmentType = false,
        };
}

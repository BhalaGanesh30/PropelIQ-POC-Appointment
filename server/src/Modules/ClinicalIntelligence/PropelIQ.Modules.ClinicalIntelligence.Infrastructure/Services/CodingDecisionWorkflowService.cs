using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Application.AiAudit;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Orchestrates the Accept, Modify, and Reject coding decision workflow (US_051).
///
/// Workflow per mutation:
///   1. Load the decision row to verify existence and snapshot the original code (Modify only).
///   2. Guard against a submitted encounter (Edge Case 1 → HTTP 409).
///   3. Atomically transition <c>reviewer_action</c> from Pending; 0 rows = already decided → HTTP 409.
///   4. Write immutable audit record via <see cref="IAuditService.LogEventAsync"/> (NFR-010, DR-005).
///   5. Invalidate Redis cache for suggestion and pending queues.
/// </summary>
internal sealed class CodingDecisionWorkflowService : ICodingDecisionWorkflowService
{
    private readonly ICodingDecisionRepository _decisionRepo;
    private readonly IAuditService             _auditService;
    private readonly IAiAuditService           _aiAuditService;
    private readonly ICacheService             _cache;
    private readonly AppDbContext              _db;
    private readonly ILogger<CodingDecisionWorkflowService> _logger;

    public CodingDecisionWorkflowService(
        ICodingDecisionRepository decisionRepo,
        IAuditService             auditService,
        IAiAuditService           aiAuditService,
        ICacheService             cache,
        AppDbContext              db,
        ILogger<CodingDecisionWorkflowService> logger)
    {
        _decisionRepo   = decisionRepo;
        _auditService   = auditService;
        _aiAuditService = aiAuditService;
        _cache          = cache;
        _db             = db;
        _logger         = logger;
    }

    /// <inheritdoc />
    public async Task AcceptAsync(Guid decisionId, Guid reviewerId, string? reviewerNote = null, CancellationToken ct = default)
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity("coding_decision.accept");
        activity?.SetTag("decision.id", decisionId);
        activity?.SetTag("reviewer_action", "accepted");

        var decision = await LoadOrThrowAsync(decisionId, ct);
        activity?.SetTag("patient.id", decision.PatientId);

        await GuardEncounterNotSubmittedAsync(decision.PatientId, ct);

        var rows = await _decisionRepo.UpdateReviewerActionAsync(
            decisionId,
            ReviewerAction.Accepted,
            reviewerId,
            finalCode:          null,
            finalDescription:   null,
            originalIcd10Code:  null,
            originalCptCode:    null,
            ct);

        if (rows == 0)
        {
            throw new InvalidOperationException($"Coding decision {decisionId} is already decided.");
        }

        DiagnosticsConfig.AcceptDecisionCounter.Add(1,
            new("decision.id", decisionId),
            new("patient.id", decision.PatientId));

        await _auditService.LogEventAsync(
            eventType:        "coding_accepted",
            actorUserId:      reviewerId,
            targetEntityId:   decisionId,
            targetEntityType: "coding_decision",
            metadata: new Dictionary<string, string>
            {
                ["decision_id"] = decisionId.ToString(),
                ["final_code"]  = decision.SuggestedCode,
            },
            ct: ct);

        // US_055: append AI audit outcome if the decision was AI-generated.
        if (decision.AiRequestId.HasValue)
        {
            await _aiAuditService.AppendReviewerOutcomeAsync(
                decision.AiRequestId.Value, "Accept", reviewerNote, ct);
        }

        await InvalidateCacheAsync(decision.PatientId, ct);
    }

    /// <inheritdoc />
    public async Task ModifyAsync(
        Guid decisionId,
        ModifyDecisionRequestDto request,
        Guid reviewerId,
        CancellationToken ct = default)
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity("coding_decision.modify");
        activity?.SetTag("decision.id", decisionId);
        activity?.SetTag("reviewer_action", "modified");

        var decision = await LoadOrThrowAsync(decisionId, ct);
        activity?.SetTag("patient.id", decision.PatientId);

        await GuardEncounterNotSubmittedAsync(decision.PatientId, ct);

        // Snapshot original AI-suggested codes before overwriting (AIR-007, task_003).
        var originalIcd10Code = decision.CodeType == "ICD10" ? decision.SuggestedCode : null;
        var originalCptCode   = decision.CptCode;
        var originalValue     = originalIcd10Code ?? originalCptCode ?? decision.SuggestedCode;

        var rows = await _decisionRepo.UpdateReviewerActionAsync(
            decisionId,
            ReviewerAction.Modified,
            reviewerId,
            finalCode:          request.FinalCode,
            finalDescription:   request.FinalDescription,
            originalIcd10Code:  originalIcd10Code,
            originalCptCode:    originalCptCode,
            ct);

        if (rows == 0)
        {
            throw new InvalidOperationException($"Coding decision {decisionId} is already decided.");
        }

        DiagnosticsConfig.ModifyDecisionCounter.Add(1,
            new("decision.id", decisionId),
            new("patient.id", decision.PatientId));

        await _auditService.LogEventAsync(
            eventType:        "coding_modified",
            actorUserId:      reviewerId,
            targetEntityId:   decisionId,
            targetEntityType: "coding_decision",
            metadata: new Dictionary<string, string>
            {
                ["decision_id"]     = decisionId.ToString(),
                ["original_value"]  = originalValue,
                ["final_value"]     = request.FinalCode,
                ["final_description"] = request.FinalDescription,
            },
            ct: ct);

        // US_055: append AI audit outcome if the decision was AI-generated.
        if (decision.AiRequestId.HasValue)
        {
            await _aiAuditService.AppendReviewerOutcomeAsync(
                decision.AiRequestId.Value, "Modify", request.ReviewerNote, ct);
        }

        await InvalidateCacheAsync(decision.PatientId, ct);
    }

    /// <inheritdoc />
    public async Task RejectAsync(Guid decisionId, Guid reviewerId, string? reviewerNote = null, CancellationToken ct = default)
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity("coding_decision.reject");
        activity?.SetTag("decision.id", decisionId);
        activity?.SetTag("reviewer_action", "rejected");

        var decision = await LoadOrThrowAsync(decisionId, ct);
        activity?.SetTag("patient.id", decision.PatientId);

        await GuardEncounterNotSubmittedAsync(decision.PatientId, ct);

        var rows = await _decisionRepo.UpdateReviewerActionAsync(
            decisionId,
            ReviewerAction.Rejected,
            reviewerId,
            finalCode:          null,
            finalDescription:   null,
            originalIcd10Code:  null,
            originalCptCode:    null,
            ct);

        if (rows == 0)
        {
            throw new InvalidOperationException($"Coding decision {decisionId} is already decided.");
        }

        DiagnosticsConfig.RejectDecisionCounter.Add(1,
            new("decision.id", decisionId),
            new("patient.id", decision.PatientId));

        await _auditService.LogEventAsync(
            eventType:        "coding_rejected",
            actorUserId:      reviewerId,
            targetEntityId:   decisionId,
            targetEntityType: "coding_decision",
            metadata: new Dictionary<string, string>
            {
                ["decision_id"] = decisionId.ToString(),
            },
            ct: ct);

        // US_055: append AI audit outcome if the decision was AI-generated.
        if (decision.AiRequestId.HasValue)
        {
            await _aiAuditService.AppendReviewerOutcomeAsync(
                decision.AiRequestId.Value, "Reject", reviewerNote, ct);
        }

        await InvalidateCacheAsync(decision.PatientId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingDecisionDto>> GetPendingAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        return await _decisionRepo.GetPendingByPatientAsync(patientId, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Loads a coding decision by ID or throws <see cref="KeyNotFoundException"/> (→ HTTP 404).
    /// </summary>
    private async Task<Domain.Entities.CodingDecision> LoadOrThrowAsync(
        Guid decisionId,
        CancellationToken ct)
    {
        var decision = await _decisionRepo.GetByIdAsync(decisionId, ct);
        if (decision is null)
        {
            throw new KeyNotFoundException($"Coding decision {decisionId} was not found.");
        }
        return decision;
    }

    /// <summary>
    /// Throws <see cref="EncounterAlreadySubmittedException"/> when the patient's most recent
    /// appointment has already been submitted for billing (Edge Case 1).
    /// </summary>
    private async Task GuardEncounterNotSubmittedAsync(Guid patientId, CancellationToken ct)
    {
        var submittedStatus = AppointmentStatus.Submitted.ToString();
        var hasSubmitted = await _db.Appointments
            .AnyAsync(a => a.PatientId == patientId && a.Status == submittedStatus, ct);

        if (hasSubmitted)
        {
            throw new EncounterAlreadySubmittedException(patientId);
        }
    }

    /// <summary>
    /// Invalidates Redis cache entries for the patient's ICD-10 suggestions and pending queue.
    /// Best-effort — a Redis failure here must not block the primary write.
    /// </summary>
    private async Task InvalidateCacheAsync(Guid patientId, CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                _cache.RemoveAsync($"coding-suggestion:{patientId}", ct),
                _cache.RemoveAsync($"cpt-suggestion:{patientId}:*", ct),
                _cache.RemoveAsync($"pending:{patientId}", ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Non-fatal: Redis cache invalidation failed for patient {PatientId} after coding decision update.",
                patientId);
        }
    }
}

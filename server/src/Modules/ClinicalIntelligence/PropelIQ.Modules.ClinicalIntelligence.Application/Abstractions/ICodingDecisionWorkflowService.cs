using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Orchestrates the Accept, Modify, and Reject coding decision workflow (US_051).
///
/// Each mutation method:
///   1. Guards against a submitted encounter (Edge Case 1 → HTTP 409).
///   2. Atomically transitions the decision from Pending to the target state.
///   3. Writes an immutable audit record (NFR-010, DR-005).
///   4. Invalidates related Redis cache entries for suggestion and pending queues.
/// </summary>
public interface ICodingDecisionWorkflowService
{
    /// <summary>
    /// Records that the clinician accepted the AI-suggested code as-is (AC-1).
    ///
    /// Audit event: <c>coding_accepted</c> with <c>final_code</c> in metadata.
    /// </summary>
    /// <param name="decisionId">Primary key of the coding decision.</param>
    /// <param name="reviewerId">Authenticated clinician's user ID.</param>
    /// <param name="reviewerNote">Optional clinician note stored in AI audit outcome (US_055, AC-2).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Exceptions.EncounterAlreadySubmittedException">When encounter is already submitted.</exception>
    Task AcceptAsync(Guid decisionId, Guid reviewerId, string? reviewerNote = null, CancellationToken ct = default);

    /// <summary>
    /// Records that the clinician accepted the suggestion with a modified code value (AC-2).
    ///
    /// Snapshots the original AI code into <c>original_icd10_code</c> / <c>original_cpt_code</c>
    /// before overwriting with the clinician-supplied final code (AIR-007 agreement tracking).
    /// Audit event: <c>coding_modified</c> with <c>original_value</c> and <c>final_value</c>.
    /// </summary>
    /// <param name="decisionId">Primary key of the coding decision.</param>
    /// <param name="request">Clinician-supplied final code and description.</param>
    /// <param name="reviewerId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Exceptions.EncounterAlreadySubmittedException">When encounter is already submitted.</exception>
    Task ModifyAsync(Guid decisionId, ModifyDecisionRequestDto request, Guid reviewerId, CancellationToken ct = default);

    /// <summary>
    /// Records that the clinician rejected the AI suggestion without applying any code (AC-3).
    ///
    /// Audit event: <c>coding_rejected</c> with <c>decision_id</c> in metadata.
    /// </summary>
    /// <param name="decisionId">Primary key of the coding decision.</param>
    /// <param name="reviewerId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Exceptions.EncounterAlreadySubmittedException">When encounter is already submitted.</exception>
    Task RejectAsync(Guid decisionId, Guid reviewerId, string? reviewerNote = null, CancellationToken ct = default);

    /// <summary>
    /// Returns all pending coding decisions for a patient (AC-4 submission block).
    ///
    /// Used by the FE to populate the "Coding decisions required" banner when
    /// the clinician attempts to submit the encounter for billing.
    /// </summary>
    /// <param name="patientId">Patient whose pending decisions to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PendingDecisionDto>> GetPendingAsync(Guid patientId, CancellationToken ct = default);
}

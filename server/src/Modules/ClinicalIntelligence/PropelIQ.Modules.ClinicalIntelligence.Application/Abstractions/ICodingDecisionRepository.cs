using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for <see cref="CodingDecision"/> persistence.
/// </summary>
public interface ICodingDecisionRepository
{
    /// <summary>
    /// Returns <c>true</c> when at least one <c>coding_decisions</c> row references
    /// the given fact (US_047 Edge Case 2 — edit allowed but FE should show amber warning).
    /// </summary>
    /// <param name="factId">Primary key of the clinical fact.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ExistsForFactAsync(Guid factId, CancellationToken ct = default);

    // ── US_049 additions ──────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-inserts pending coding decision rows — one per AI-generated suggestion.
    /// Sets <c>reviewer_action = "Pending"</c> on each row and returns the generated GUIDs.
    /// (US_049, AC-1).
    /// </summary>
    Task<IReadOnlyList<Guid>> InsertPendingAsync(
        IEnumerable<CodingDecision> decisions,
        CancellationToken ct = default);

    // ── US_051 additions ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns a single <c>coding_decisions</c> row by primary key, or <c>null</c> if not found.
    /// Used by the workflow service to snapshot the original code before a Modify action.
    /// </summary>
    /// <param name="decisionId">Primary key of the coding decision.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CodingDecision?> GetByIdAsync(Guid decisionId, CancellationToken ct = default);

    /// <summary>
    /// Atomically updates reviewer fields on a <c>coding_decisions</c> row,
    /// but only if the row is currently in <c>reviewer_action = 'Pending'</c> state.
    ///
    /// Returns the number of rows affected:
    ///   - 1 = update succeeded (decision was still pending).
    ///   - 0 = decision not found or already decided → caller should return HTTP 409.
    ///
    /// On Modify, <paramref name="originalCode"/> is stored in
    /// <c>original_icd10_code</c> or <c>original_cpt_code</c> (task_003 columns)
    /// before the finalized code overwrites <c>suggested_code</c> / <c>cpt_code</c>.
    /// </summary>
    /// <param name="decisionId">Primary key of the decision to update.</param>
    /// <param name="action">The new reviewer action state.</param>
    /// <param name="reviewerId">Authenticated clinician's user ID.</param>
    /// <param name="finalCode">Finalized code value (null for Accept/Reject).</param>
    /// <param name="finalDescription">Finalized description (null for Accept/Reject).</param>
    /// <param name="originalIcd10Code">Original AI ICD-10 code snapshot (Modify only, null otherwise).</param>
    /// <param name="originalCptCode">Original AI CPT code snapshot (Modify only, null otherwise).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<int> UpdateReviewerActionAsync(
        Guid decisionId,
        ReviewerAction action,
        Guid reviewerId,
        string? finalCode,
        string? finalDescription,
        string? originalIcd10Code,
        string? originalCptCode,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all <c>coding_decisions</c> rows for the given patient where
    /// <c>reviewer_action = 'Pending'</c>, ordered by <c>created_at</c> ascending.
    /// Used to populate the AC-4 submission block banner (US_051).
    /// </summary>
    /// <param name="patientId">Patient whose pending decisions to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PendingDecisionDto>> GetPendingByPatientAsync(
        Guid patientId,
        CancellationToken ct = default);
}

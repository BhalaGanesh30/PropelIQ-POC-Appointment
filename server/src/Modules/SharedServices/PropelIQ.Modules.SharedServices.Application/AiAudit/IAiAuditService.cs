namespace PropelIQ.Modules.SharedServices.Application.AiAudit;

/// <summary>
/// Append-only AI audit service (US_055, AIR-011).
///
/// Callers use <see cref="LogAiRequestAsync"/> for fire-and-forget audit logging after each
/// AI gateway call, and <see cref="AppendReviewerOutcomeAsync"/> when a clinician accepts,
/// modifies, or rejects a coding suggestion.  Neither method ever updates or deletes rows —
/// every action is a new INSERT (AC-3, DR-005).
///
/// On transient write failure, <see cref="LogAiRequestAsync"/> writes to the outbox
/// (<c>ai_audit_outbox</c>) so the record is not silently lost (Edge Case 1).
/// </summary>
public interface IAiAuditService
{
    /// <summary>
    /// Persists an AI request audit record.  Failures fall back to the outbox — callers
    /// should not await exceptions from this method for non-critical flows.
    /// </summary>
    /// <param name="entry">Audit payload — all fields are required.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LogAiRequestAsync(AiAuditEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Appends a reviewer decision outcome linked to the AI request.
    /// Called by <c>CodingDecisionWorkflowService</c> on Accept / Modify / Reject.
    /// </summary>
    /// <param name="aiRequestId">
    /// FK to <c>ai_audit_logs.ai_request_id</c>; guarded by caller (skip if null).
    /// </param>
    /// <param name="reviewerAction">String representation of the action ("Accept", "Modify", "Reject").</param>
    /// <param name="reviewerNote">Optional clinician note (verbatim; stored max 2000 chars).</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendReviewerOutcomeAsync(
        Guid            aiRequestId,
        string          reviewerAction,
        string?         reviewerNote,
        CancellationToken ct = default);

    /// <summary>
    /// Returns paginated AI audit log entries for the admin endpoint (AC-4).
    /// Filtered by optional <paramref name="clinicianId"/> and date range.
    /// Results are ordered by <c>request_timestamp</c> descending.
    /// </summary>
    /// <param name="clinicianId">Filter by clinician. Null returns records for all clinicians.</param>
    /// <param name="from">Inclusive lower bound (UTC). Null = no lower bound.</param>
    /// <param name="to">Inclusive upper bound (UTC). Null = no upper bound.</param>
    /// <param name="pageSize">Page size (1–200, default 50).</param>
    /// <param name="page">0-based page index (default 0).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<AiAuditLogDto>> QueryAsync(
        Guid?           clinicianId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int             pageSize,
        int             page,
        CancellationToken ct = default);
}

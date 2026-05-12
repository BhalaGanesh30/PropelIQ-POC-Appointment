namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Append-only reviewer decision outcome linked to an AI audit log entry (US_055, AC-2).
///
/// Persisted in <c>app.ai_audit_log_outcomes</c>. A separate table (not an UPDATE on
/// <c>ai_audit_logs</c>) preserves the append-only constraint of the base record (AC-3, DR-005).
///
/// Multiple outcomes per <see cref="AiRequestId"/> are permitted — e.g., accept after prior modify.
/// </summary>
public sealed class AiAuditLogOutcomeEntity
{
    /// <summary>Auto-generated surrogate PK.</summary>
    public Guid OutcomeId { get; init; } = Guid.NewGuid();

    /// <summary>FK to <see cref="AiAuditLogEntity.AiRequestId"/> — links the outcome to the AI request.</summary>
    public required Guid AiRequestId { get; init; }

    /// <summary>Accept | Modify | Reject (string value of <c>ReviewerAction</c> enum).</summary>
    public required string ReviewerAction { get; init; }

    /// <summary>Optional clinician-supplied note (verbatim — not modified or redacted).</summary>
    public string? ReviewerNote { get; init; }

    /// <summary>UTC timestamp when the clinician recorded the decision.</summary>
    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.UtcNow;
}

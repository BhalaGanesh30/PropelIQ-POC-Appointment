namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Append-only audit event per DR-005. Does not inherit BaseEntity
/// because audit records are immutable after creation — no UpdatedAt needed.
/// </summary>
public sealed class AuditRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string EventType { get; init; }
    public required Guid ActorUserId { get; init; }
    public Guid? TargetEntityId { get; init; }
    public required string TargetEntityType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public AuditDetails Details { get; init; } = new();

    // ── Override-specific columns (EP-004 US_034, task_003) ─────────────────────
    // All nullable so pre-existing audit records retain NULL values (DR-007).

    /// <summary>
    /// Machine-readable constraint type bypassed during the override
    /// (e.g., "CancellationWithin24Hours"). Null for non-override events.
    /// </summary>
    public string? OverrideConstraintType { get; init; }

    /// <summary>
    /// Staff-provided justification text (max 500 chars). Null for non-override events.
    /// Stored verbatim per NFR-010 (immutable once written).
    /// </summary>
    public string? OverrideReason { get; init; }

    /// <summary>
    /// Override action taken (e.g., "Cancel", "Reschedule", "ForceBook").
    /// Null for non-override events.
    /// </summary>
    public string? OverrideAction { get; init; }
}

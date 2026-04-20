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
}

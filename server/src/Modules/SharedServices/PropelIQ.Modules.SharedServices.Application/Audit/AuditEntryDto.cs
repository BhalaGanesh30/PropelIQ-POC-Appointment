namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Read model returned by <see cref="IAuditService.GetAuditEntriesAsync"/> (AC-4).
///
/// Maps <see cref="PropelIQ.Modules.SharedServices.Domain.Entities.AuditRecord"/> plus
/// a join to the Users table for the actor display name and role.
/// </summary>
public sealed class AuditEntryDto
{
    public Guid AuditId { get; init; }
    public required string EventType { get; init; }

    /// <summary>UUID of the staff member who performed the action.</summary>
    public Guid ActorUserId { get; init; }

    /// <summary>
    /// Display name resolved from the Users table.
    /// Null when the user record has been deleted (soft-delete tombstone).
    /// </summary>
    public string? ActorName { get; init; }

    /// <summary>Role of the actor at the time of the event.</summary>
    public string? ActorRole { get; init; }

    public Guid? TargetEntityId { get; init; }
    public required string TargetEntityType { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Structured metadata extracted from <c>AuditDetails.Metadata</c>.
    /// For override events: includes <c>constraintType</c>, <c>reason</c>,
    /// <c>action</c>, <c>overrideRecordId</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}

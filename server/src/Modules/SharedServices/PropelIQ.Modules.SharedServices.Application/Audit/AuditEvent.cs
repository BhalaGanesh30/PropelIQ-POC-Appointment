namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Immutable payload emitted by any module for append-only audit logging (US_056, AC-1).
///
/// Passed to <see cref="IAuditRecordService.WriteAsync"/> which routes it through a
/// bounded in-process channel to <c>AuditRecordWriterWorker</c> for non-blocking
/// persistence to <c>app.audit_records</c>.
///
/// Fields map 1-to-1 to <c>AuditRecord</c> columns. No PII should appear in
/// <see cref="Details"/> values — actors are identified by UUID, not name (AC-2, DR-005).
/// </summary>
public sealed record AuditEvent
{
    /// <summary>UUID of the authenticated user who triggered the event.</summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Machine-readable event type (max 50 chars).
    /// Examples: <c>"DataAccess"</c>, <c>"ConfigChanged"</c>, <c>"RoleAssigned"</c>,
    /// <c>"LoginSuccess"</c>, <c>"LoginFailure"</c>, <c>"Override"</c>.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Type name of the affected entity (e.g., <c>"Patient"</c>, <c>"Appointment"</c>).
    /// Max 100 chars.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>UUID of the affected entity. Null for events with no specific target entity.</summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// Structured key-value detail payload for forensic traceability (AC-1).
    /// Values are serialized to JSONB. Do not include raw PII — use entity IDs only.
    /// Max 20 entries enforced at write time.
    /// </summary>
    public required Dictionary<string, object> Details { get; init; }

    /// <summary>UTC timestamp of the event occurrence. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

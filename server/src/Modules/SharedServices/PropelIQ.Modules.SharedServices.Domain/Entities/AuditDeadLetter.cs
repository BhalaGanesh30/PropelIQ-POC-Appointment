namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Dead-letter entry for an audit event that could not be persisted after all retries (US_056, AC-2).
///
/// Does not inherit <c>BaseEntity</c> — it is mutable (retry tracking), append-only from a
/// compliance perspective, and requires its own field semantics separate from domain entities.
/// Dead letters are never deleted — resolved entries are marked with <see cref="ResolvedAt"/>
/// to maintain a full forensic trail (DR-005, NFR-010).
/// </summary>
public sealed class AuditDeadLetter
{
    /// <summary>Unique dead-letter record ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>JSON-serialized <c>AuditEvent</c> payload that failed to write.</summary>
    public required string Payload { get; init; }

    /// <summary>Original exception message that caused the write failure (max 2000 chars).</summary>
    public required string ErrorMessage { get; init; }

    /// <summary>UTC timestamp when the event was moved to the dead-letter table.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Total number of retry attempts made so far. Starts at 0.</summary>
    public int RetryCount { get; set; }

    /// <summary>UTC timestamp of the most recent retry attempt. Null if no retry yet.</summary>
    public DateTimeOffset? LastRetryAt { get; set; }

    /// <summary>
    /// UTC timestamp when the entry was successfully replayed and resolved.
    /// Null while unresolved. Used by the filtered index and <c>DeadLetterRetryWorker</c>.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }
}

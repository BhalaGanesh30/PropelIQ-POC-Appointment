namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Compensating retry buffer for failed <c>ai_audit_logs</c> writes (US_055, Edge Case 1).
///
/// When the primary <c>AiAuditService.LogAiRequestAsync</c> write fails (DB unavailable,
/// transient error), the serialized <see cref="Payload"/> is inserted here and processed
/// by <c>AiAuditOutboxProcessor</c> (IHostedService) at 60-second intervals.
///
/// After 3 retries (<see cref="RetryCount"/> &gt;= 3) the record is not deleted;
/// instead an operations alert is emitted via the <c>compliance.audit_write_failure</c>
/// OpenTelemetry counter for manual investigation (Edge Case 1, AC-3).
/// </summary>
public sealed class AiAuditOutboxEntity
{
    /// <summary>Auto-generated surrogate PK.</summary>
    public Guid OutboxId { get; init; } = Guid.NewGuid();

    /// <summary>Correlation ID of the failed AI request (for tracing log entries).</summary>
    public required Guid AiRequestId { get; init; }

    /// <summary>
    /// JSON-serialized <c>AiAuditEntry</c> payload — replayed by the outbox processor
    /// on each retry attempt.
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>Number of retry attempts made so far. Outbox processor increments this on each failure.</summary>
    public int RetryCount { get; set; }

    /// <summary>UTC timestamp of the most recent retry attempt. Null until first attempt.</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>UTC timestamp when the outbox record was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

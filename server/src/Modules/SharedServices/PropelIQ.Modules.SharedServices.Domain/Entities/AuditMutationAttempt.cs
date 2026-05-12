namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Records a rejected UPDATE or DELETE attempt against the audit tables (US_056, AC-2).
///
/// Written at the application layer by <c>AuditRecordWriterWorker</c> when it catches
/// a mutation attempt (e.g., a <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>
/// caused by the immutability trigger), complementing the database-level pgaudit log.
///
/// This table is append-only at the application layer — no updates or deletes.
/// </summary>
public sealed class AuditMutationAttempt
{
    /// <summary>Unique record ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Identity of the actor who attempted the mutation (user ID string or "System").</summary>
    public required string AttemptedBy { get; init; }

    /// <summary>Operation that was attempted: <c>"UPDATE"</c> or <c>"DELETE"</c>.</summary>
    public required string Operation { get; init; }

    /// <summary>UUID of the audit record that the actor tried to mutate. Null if unknown.</summary>
    public Guid? TargetAuditId { get; init; }

    /// <summary>Exception or trigger message returned by PostgreSQL (max 2000 chars).</summary>
    public required string ErrorMessage { get; init; }

    /// <summary>UTC timestamp when the mutation was attempted.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Optional client IP address from the HTTP context (max 45 chars for IPv6).</summary>
    public string? SourceIp { get; init; }
}

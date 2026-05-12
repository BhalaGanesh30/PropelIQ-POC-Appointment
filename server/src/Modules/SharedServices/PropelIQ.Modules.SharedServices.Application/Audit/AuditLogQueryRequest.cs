namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Filter + pagination parameters for the admin audit-log query endpoint (US_056, AC-3).
/// All filter properties are optional — omitted values return all records in that dimension.
/// </summary>
public sealed class AuditLogQueryRequest
{
    /// <summary>Filter by exact actor user ID.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>Filter by event type string (e.g., <c>"DataAccess"</c>, <c>"ConfigChanged"</c>).</summary>
    public string? EventType { get; init; }

    /// <summary>Inclusive lower bound for <c>occurred_at</c>.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Inclusive upper bound for <c>occurred_at</c>.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Filter by target entity UUID.</summary>
    public Guid? EntityId { get; init; }

    /// <summary>Zero-based page number. Defaults to 0.</summary>
    public int Page { get; init; } = 0;

    /// <summary>Page size. Clamped to 1–200. Defaults to 50.</summary>
    public int PageSize { get; init; } = 50;
}

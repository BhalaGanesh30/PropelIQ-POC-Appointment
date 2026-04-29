namespace PropelIQ.Modules.Scheduling.Application.Appointments.Dto;

/// <summary>
/// Query parameters for <c>GET /api/v1/appointments/history</c>.
/// AC-1: sorted date descending.
/// AC-2: optional status filter returns results within 500 ms (NFR-002 via composite index).
/// AC-3: optional date-range filter.
/// Edge case: default page size 20 for patients with hundreds of records.
/// </summary>
public record AppointmentHistoryFilter
{
    public string? Status { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// A single appointment in the history list response.
/// Maps from <see cref="PropelIQ.Modules.Scheduling.Domain.Entities.Appointment"/>.
/// </summary>
public record AppointmentHistoryItem
{
    public Guid Id { get; init; }
    /// <summary>ISO-8601 full timestamp (includes offset).</summary>
    public DateTimeOffset ScheduledAt { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    /// <summary>True when an intake record has been submitted for this appointment.</summary>
    public bool HasIntakeRecord { get; init; }
}

/// <summary>
/// Paginated response for <c>GET /api/v1/appointments/history</c>.
/// Edge case: empty history returns 200 with Items=[] and TotalCount=0.
/// </summary>
public record AppointmentHistoryResponse
{
    public List<AppointmentHistoryItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

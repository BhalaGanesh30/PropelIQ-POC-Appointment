using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Scheduling;

/// <summary>
/// Inbound query parameters for slot search. Bound from HTTP query string.
/// </summary>
public record SlotSearchQuery
{
    public DateTimeOffset DateFrom { get; init; }
    public DateTimeOffset DateTo { get; init; }
    public SlotDuration? Duration { get; init; }
    public AppointmentType? Type { get; init; }
}

/// <summary>
/// Top-level response returned by GET /api/v1/appointments/slots.
/// </summary>
public record SlotSearchResponse
{
    public List<SlotGroupDto> Days { get; init; } = [];
    public int TotalAvailableSlots { get; init; }
    public bool HasResults => TotalAvailableSlots > 0;
}

/// <summary>
/// Available slots grouped by calendar date.
/// </summary>
public record SlotGroupDto
{
    public DateOnly Date { get; init; }
    public List<SlotDto> Slots { get; init; } = [];
}

/// <summary>
/// Individual bookable slot returned to the caller.
/// </summary>
public record SlotDto
{
    public Guid Id { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public int AvailableCapacity { get; init; }
}

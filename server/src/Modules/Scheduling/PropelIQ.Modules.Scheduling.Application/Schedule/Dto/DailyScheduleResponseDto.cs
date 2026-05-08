namespace PropelIQ.Modules.Scheduling.Application.Schedule.Dto;

/// <summary>
/// Wrapper response returned by <c>GET /api/v1/schedule/daily</c> (AC-1).
/// Edge Case 2: <see cref="Entries"/> is empty when no appointments exist for the date.
/// </summary>
public sealed class DailyScheduleResponseDto
{
    /// <summary>Calendar date for which appointments are returned (yyyy-MM-dd).</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Appointment entries sorted by <see cref="DailyScheduleEntryDto.StartTime"/> ASC.</summary>
    public required IReadOnlyList<DailyScheduleEntryDto> Entries { get; init; }
    public required int TotalCount { get; init; }
}

namespace PropelIQ.Modules.Scheduling.Application.Queue.Dto;

/// <summary>
/// Wrapper response returned by <c>GET /api/v1/queue/today</c> (EP-004 US_031).
///
/// <see cref="Entries"/> is sorted ArrivedAt ASC, then ScheduledAt ASC so
/// the longest-waiting patient appears first in the dashboard table.
/// <see cref="GeneratedAt"/> is the UTC timestamp the response was computed
/// (either from cache or fresh from the database).
/// </summary>
public sealed record QueueResponseDto
{
    public required IReadOnlyList<QueueEntryDto> Entries { get; init; }
    public required int TotalCount { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}

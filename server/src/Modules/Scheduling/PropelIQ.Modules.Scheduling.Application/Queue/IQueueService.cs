using PropelIQ.Modules.Scheduling.Application.Queue.Dto;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Queue;

/// <summary>
/// Business logic contract for real-time queue data aggregation (EP-004 US_031).
/// Implemented by <c>QueueService</c> in the Infrastructure layer.
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Returns all today's appointments enriched with queue state, patient name,
    /// wait-time estimate, and overdue flag.
    ///
    /// AC-1: Response includes all non-cancelled today appointments with status badges.
    /// AC-2: Optional <paramref name="statusFilter"/> limits results to a single state.
    /// AC-3: <see cref="QueueEntryDto.IsOverdue"/> is set per entry.
    /// Edge Case 1: Redis miss falls through to PostgreSQL transparently.
    /// Edge Case 2: Invalid status string is rejected before this method is called
    ///              by the controller's model-binding validation.
    /// </summary>
    Task<QueueResponseDto> GetTodayQueueAsync(
        QueueState? statusFilter,
        CancellationToken ct);
}

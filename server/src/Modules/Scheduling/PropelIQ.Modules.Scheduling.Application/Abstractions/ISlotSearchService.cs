using PropelIQ.Modules.Scheduling.Application.Scheduling;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Cache-first slot search orchestration abstraction.
/// Implemented in the Application layer by SlotSearchService.
/// </summary>
public interface ISlotSearchService
{
    Task<SlotSearchResponse> SearchAsync(SlotSearchQuery query, CancellationToken ct);
}

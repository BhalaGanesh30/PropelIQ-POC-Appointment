using PropelIQ.Modules.Scheduling.Application.Abstractions;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Queue;

/// <summary>
/// Development stub for <see cref="IWaitTimeEstimationService"/>.
/// Returns safe zero-based defaults so no appointments are falsely flagged
/// overdue before the real service is wired in.
/// </summary>
internal sealed class StubWaitTimeEstimationService : IWaitTimeEstimationService
{
    /// <inheritdoc />
    public int CalculateEstimatedWaitMinutes(int queuePosition, string appointmentTypeCode) => 0;

    /// <inheritdoc />
    public bool IsOverdue(DateTimeOffset? arrivedAt, int estimatedWaitMinutes) => false;
}

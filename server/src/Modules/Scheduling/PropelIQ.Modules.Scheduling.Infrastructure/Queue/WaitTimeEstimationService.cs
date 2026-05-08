using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Abstractions;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Queue;

/// <summary>
/// Pure wait-time estimation service for the real-time queue dashboard
/// (EP-004 US_031 task_003).
///
/// No I/O — depends only on read-only <see cref="WaitTimeOptions"/> configuration.
/// Register as singleton.
///
/// AC-1: Estimate computation is O(1) per entry (dictionary lookup + multiply).
/// AC-3: <see cref="IsOverdue"/> compares elapsed time against the estimate.
/// Edge Case 2: O(n) algorithm guarantee — callers use LINQ Select with index
///              (single pass) before invoking <see cref="CalculateEstimatedWaitMinutes"/>.
/// </summary>
public sealed class WaitTimeEstimationService : IWaitTimeEstimationService
{
    private readonly WaitTimeOptions _options;

    public WaitTimeEstimationService(IOptions<WaitTimeOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Formula: <c>queuePosition × serviceMinutes</c> where <c>serviceMinutes</c>
    /// is looked up from <see cref="WaitTimeOptions.AppointmentTypeDurations"/> in
    /// O(1) via <see cref="Dictionary{TKey,TValue}.TryGetValue"/>; falls back to
    /// <see cref="WaitTimeOptions.DefaultServiceDurationMinutes"/> when the type is
    /// not configured.
    ///
    /// O(n) contract: each caller must supply <c>queuePosition</c> as the index from
    /// a single <c>Select((entry, index) =&gt; ...)</c> pass — no nested iteration
    /// permitted.  This keeps the overall queue-projection algorithm O(n).
    /// </remarks>
    public int CalculateEstimatedWaitMinutes(int queuePosition, string appointmentTypeCode)
    {
        if (!_options.AppointmentTypeDurations.TryGetValue(
                appointmentTypeCode,
                out var duration))
        {
            duration = _options.DefaultServiceDurationMinutes;
        }

        return queuePosition * duration;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns <see langword="false" /> when <paramref name="arrivedAt"/> is
    /// <see langword="null" /> — a patient who has not yet checked in cannot be
    /// overdue (pre-task_004 migration: ArrivedAt column not yet populated).
    /// </remarks>
    public bool IsOverdue(DateTimeOffset? arrivedAt, int estimatedWaitMinutes)
    {
        if (!arrivedAt.HasValue) return false;

        var elapsed = DateTimeOffset.UtcNow - arrivedAt.Value;
        return elapsed > TimeSpan.FromMinutes(estimatedWaitMinutes);
    }
}

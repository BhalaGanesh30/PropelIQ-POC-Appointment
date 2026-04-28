using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Abstraction over the channel-specific notification delivery pipeline.
/// Implementations wrap the underlying <c>INotificationSender</c> with
/// retry/timeout resilience (Polly) and route by <see cref="ReminderEvent.Channel"/>.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches the reminder notification for the given event's channel (Email or Sms).
    /// Throws on non-transient failure or when retry budget is exhausted.
    /// </summary>
    Task DispatchAsync(ReminderEvent reminder, CancellationToken ct = default);
}

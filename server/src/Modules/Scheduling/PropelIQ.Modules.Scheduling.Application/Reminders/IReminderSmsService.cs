using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Reminder-specific SMS delivery abstraction.
/// Implementations construct concise SMS content with a short-link
/// and delegate to the underlying SMS provider (Twilio).
/// </summary>
public interface IReminderSmsService
{
    /// <summary>
    /// AC-2: Sends a concise reminder SMS for the given event.
    /// </summary>
    Task SendReminderSmsAsync(
        ReminderEvent reminder,
        string phoneNumber,
        Appointment appointment,
        CancellationToken ct = default);
}

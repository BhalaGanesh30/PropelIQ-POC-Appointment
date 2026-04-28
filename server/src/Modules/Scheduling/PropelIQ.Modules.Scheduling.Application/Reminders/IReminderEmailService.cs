using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Reminder-specific email delivery abstraction.
/// Implementations construct rich HTML email content with appointment details,
/// confirm/cancel action links, and delegate to the underlying email provider (SendGrid).
/// </summary>
public interface IReminderEmailService
{
    /// <summary>
    /// AC-1: Sends a formatted reminder email for the given event.
    /// </summary>
    Task SendReminderEmailAsync(
        ReminderEvent reminder,
        string recipientEmail,
        Appointment appointment,
        CancellationToken ct = default);
}

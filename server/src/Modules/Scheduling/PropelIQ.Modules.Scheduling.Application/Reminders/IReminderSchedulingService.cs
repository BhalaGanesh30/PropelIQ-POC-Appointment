namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Manages the reminder event lifecycle for appointments.
/// Triggered by booking domain events via the existing event handlers.
/// </summary>
public interface IReminderSchedulingService
{
    /// <summary>
    /// AC-1: Creates up to four ReminderEvent rows (7d, 2d, 1d, 2h before start)
    /// per enabled patient channel. Reminders whose ScheduledAt is already in the
    /// past at creation time are silently skipped.
    /// </summary>
    Task ScheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset appointmentStart,
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// AC-3: Bulk-cancels all Pending reminders for the given appointment.
    /// </summary>
    Task CancelRemindersAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>
    /// AC-4: Cancels existing Pending reminders then creates new ones
    /// relative to the updated appointment start time within the same call.
    /// </summary>
    Task RescheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset newAppointmentStart,
        Guid patientId,
        CancellationToken ct = default);
}

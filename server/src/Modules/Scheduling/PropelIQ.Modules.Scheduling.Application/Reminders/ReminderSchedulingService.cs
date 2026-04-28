using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Core reminder lifecycle service.
/// Orchestrates creation, cancellation, and rescheduling of <see cref="ReminderEvent"/> rows.
///
/// Idempotency: duplicate events are blocked by <see cref="IReminderEventRepository.AddRangeAsync"/>
/// which checks <see cref="ReminderEvent.IdempotencyKey"/> before inserting.
/// Past-time guard: reminders whose ScheduledAt ≤ now at creation time are silently skipped
/// (e.g., a booking made 1 day before start should not create 7-day or 2-day reminders).
/// </summary>
public sealed class ReminderSchedulingService : IReminderSchedulingService
{
    private readonly IReminderEventRepository _repository;
    private readonly IPatientPreferenceRepository _preferences;
    private readonly TimeProvider _timeProvider;

    public ReminderSchedulingService(
        IReminderEventRepository repository,
        IPatientPreferenceRepository preferences,
        TimeProvider timeProvider)
    {
        _repository  = repository;
        _preferences = preferences;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task ScheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset appointmentStart,
        Guid patientId,
        CancellationToken ct = default)
    {
        var channels = await _preferences.GetEnabledChannelsAsync(patientId, ct);
        var now = _timeProvider.GetUtcNow();
        var reminders = new List<ReminderEvent>();

        foreach (var offset in ReminderOffsets.All)
        {
            var scheduledAt = appointmentStart - offset;

            // Skip reminders whose scheduled dispatch time has already passed.
            if (scheduledAt <= now)
                continue;

            foreach (var channel in channels)
            {
                reminders.Add(new ReminderEvent
                {
                    AppointmentId  = appointmentId,
                    Channel        = channel,
                    SendStatus     = ReminderSendStatus.Pending,
                    ScheduledAt    = scheduledAt,
                    RetryCount     = 0,
                    IdempotencyKey = ReminderOffsets.BuildIdempotencyKey(appointmentId, offset, channel)
                });
            }
        }

        if (reminders.Count > 0)
        {
            await _repository.AddRangeAsync(reminders, ct);
        }
    }

    /// <inheritdoc/>
    public async Task CancelRemindersAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        await _repository.CancelPendingByAppointmentAsync(appointmentId, ct);
    }

    /// <inheritdoc/>
    public async Task RescheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset newAppointmentStart,
        Guid patientId,
        CancellationToken ct = default)
    {
        // Cancel existing pending reminders first, then create new ones for the updated time.
        await _repository.CancelPendingByAppointmentAsync(appointmentId, ct);
        await ScheduleRemindersAsync(appointmentId, newAppointmentStart, patientId, ct);
    }
}

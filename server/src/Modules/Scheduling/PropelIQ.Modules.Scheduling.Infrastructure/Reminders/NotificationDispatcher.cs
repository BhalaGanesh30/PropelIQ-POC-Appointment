using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// Routes <see cref="ReminderEvent"/> dispatch to the correct channel-specific
/// provider: <see cref="IReminderEmailService"/> (SendGrid) for Email,
/// <see cref="IReminderSmsService"/> (Twilio) for Sms.
///
/// Wraps each send in a Polly resilience pipeline with exponential backoff
/// (Decision 6: 2 inner retries + 10-second timeout per attempt).
///
/// Resolves patient contact info and appointment data from <see cref="AppDbContext"/>
/// since <c>ReminderEvent</c> navigation properties are not eagerly loaded
/// during the dispatch worker's batch query.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IReminderEmailService _emailService;
    private readonly IReminderSmsService _smsService;
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationDispatcher> _logger;

    /// <summary>
    /// Polly v8 resilience pipeline: 2 automatic retries with 1-second
    /// exponential backoff and a 10-second per-attempt timeout.
    /// Handles transient I/O and timeout failures from email/SMS providers.
    /// </summary>
    private static readonly ResiliencePipeline ResiliencePipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromSeconds(1),
                ShouldHandle     = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<IOException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();

    public NotificationDispatcher(
        IReminderEmailService emailService,
        IReminderSmsService smsService,
        AppDbContext db,
        ILogger<NotificationDispatcher> logger)
    {
        _emailService = emailService;
        _smsService   = smsService;
        _db           = db;
        _logger       = logger;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(ReminderEvent reminder, CancellationToken ct = default)
    {
        // Resolve Appointment data (nav property not eagerly loaded).
        var appointment = await _db.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == reminder.AppointmentId, ct);

        if (appointment is null)
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} not found for reminder {ReminderId}; skipping dispatch.",
                reminder.AppointmentId, reminder.Id);
            return;
        }

        // Resolve recipient contact info from Patient → User.
        var contact = await _db.Patients
            .Include(p => p.User)
            .Where(p => p.UserId == appointment.PatientId)
            .Select(p => new { p.User.Email, p.ContactPreferences.PreferredPhone })
            .FirstOrDefaultAsync(ct);

        await ResiliencePipeline.ExecuteAsync(async token =>
        {
            switch (reminder.Channel)
            {
                case "Email":
                    var email = contact?.Email;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogWarning(
                            "No email address for reminder {ReminderId} " +
                            "(AppointmentId {AppointmentId}); skipping Email dispatch.",
                            reminder.Id, reminder.AppointmentId);
                        return;
                    }

                    await _emailService.SendReminderEmailAsync(
                        reminder, email, appointment, token);
                    break;

                case "Sms":
                    var phone = contact?.PreferredPhone;
                    if (string.IsNullOrWhiteSpace(phone))
                    {
                        _logger.LogWarning(
                            "No phone number for reminder {ReminderId} " +
                            "(AppointmentId {AppointmentId}); skipping Sms dispatch.",
                            reminder.Id, reminder.AppointmentId);
                        return;
                    }

                    await _smsService.SendReminderSmsAsync(
                        reminder, phone, appointment, token);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown channel '{Channel}' for reminder {ReminderId}; skipping.",
                        reminder.Channel, reminder.Id);
                    break;
            }
        }, ct);
    }
}

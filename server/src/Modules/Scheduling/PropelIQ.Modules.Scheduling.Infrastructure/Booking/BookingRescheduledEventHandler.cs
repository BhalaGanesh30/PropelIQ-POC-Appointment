using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Domain.Events;
using System.Threading.Channels;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Background worker that consumes <see cref="BookingRescheduledEvent"/> messages from
/// the in-process channel and delivers an updated ICS file via reschedule email.
///
/// AC-3 (US_024): generated ICS contains the incremented SEQUENCE so Google Calendar,
/// Outlook, and Apple Calendar recognise the event as an update (not a duplicate).
///
/// Design: same scoped-via-IServiceScopeFactory pattern as
/// <see cref="BookingConfirmedEventHandler"/> — BackgroundService is singleton while
/// IcsGenerator, IArtifactStorage, and IConfirmationEmailService are scoped.
/// Edge case: ICS generation failure is non-blocking; the event is fully logged.
/// </summary>
public sealed class BookingRescheduledEventHandler : BackgroundService
{
    private readonly Channel<BookingRescheduledEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingRescheduledEventHandler> _logger;

    public BookingRescheduledEventHandler(
        Channel<BookingRescheduledEvent> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingRescheduledEventHandler> logger)
    {
        _channel     = channel;
        _scopeFactory = scopeFactory;
        _logger      = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("BookingRescheduledEventHandler started.");

        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await ProcessAsync(evt, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Edge case: email / ICS failure must never affect the confirmed reschedule.
                    _logger.LogError(ex,
                        "Failed to process BookingRescheduledEvent for appointment {AppointmentId}. " +
                        "Reschedule remains valid.",
                        evt.AppointmentId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown — host cancellation token fired.
        }

        _logger.LogInformation("BookingRescheduledEventHandler stopped.");
    }

    private async Task ProcessAsync(BookingRescheduledEvent evt, CancellationToken ct)
    {
        using var scope        = _scopeFactory.CreateScope();
        var icsGenerator       = scope.ServiceProvider.GetRequiredService<IcsGenerator>();
        var artifactStorage    = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
        var emailService       = scope.ServiceProvider.GetRequiredService<IConfirmationEmailService>();

        byte[]? icsBytes = null;
        string? icsPath  = null;

        // AC-3: generate updated ICS with incremented SEQUENCE — non-blocking on failure.
        try
        {
            icsBytes = icsGenerator.GenerateUpdateIcs(
                appointmentId:   evt.AppointmentId,
                appointmentType: evt.AppointmentType,
                providerName:    evt.ProviderName,
                confirmationCode: evt.ConfirmationCode,
                newStartTime:    evt.NewTime,
                durationMinutes: evt.DurationMinutes,
                location:        evt.Location,
                sequenceNumber:  evt.SequenceNumber);

            icsPath = await artifactStorage.StoreAsync(
                $"bookings/{evt.AppointmentId}",
                "appointment.ics",
                icsBytes,
                "text/calendar",
                ct);

            _logger.LogInformation(
                "Updated ICS generated for rescheduled appointment {AppointmentId} (SEQUENCE={Seq})",
                evt.AppointmentId, evt.SequenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Updated ICS generation failed for appointment {AppointmentId}. " +
                "Reschedule email will be sent without ICS attachment.",
                evt.AppointmentId);
        }

        // Send reschedule confirmation email (with ICS bytes if available).
        await emailService.SendRescheduleConfirmationAsync(evt, icsBytes, ct);

        _logger.LogInformation(
            "Reschedule notification dispatched for appointment {AppointmentId} [IcsStored={HasIcs}]",
            evt.AppointmentId, icsPath is not null);
    }
}

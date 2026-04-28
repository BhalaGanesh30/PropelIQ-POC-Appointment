using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Events;
using System.Threading.Channels;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Background worker that consumes <see cref="BookingCancelledEvent"/> messages from
/// the in-process channel and delivers a cancellation ICS via email.
///
/// AC-4 (US_024): generated ICS contains STATUS:CANCELLED and METHOD:CANCEL so
/// Google Calendar, Outlook, and Apple Calendar automatically remove the event.
/// The SEQUENCE is incremented by one beyond the stored value to mark this as
/// the authoritative final version per RFC 5545 §3.8.7.4.
///
/// Design: same scoped-via-IServiceScopeFactory pattern as
/// <see cref="BookingConfirmedEventHandler"/> — BackgroundService is singleton while
/// scoped dependencies are resolved per event.
/// Edge case: ICS generation failure is non-blocking; cancellation email is still sent.
/// </summary>
public sealed class BookingCancelledEventHandler : BackgroundService
{
    private readonly Channel<BookingCancelledEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingCancelledEventHandler> _logger;

    public BookingCancelledEventHandler(
        Channel<BookingCancelledEvent> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingCancelledEventHandler> logger)
    {
        _channel     = channel;
        _scopeFactory = scopeFactory;
        _logger      = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("BookingCancelledEventHandler started.");

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
                    // Edge case: email / ICS failure must never affect the committed cancellation.
                    _logger.LogError(ex,
                        "Failed to process BookingCancelledEvent for appointment {AppointmentId}. " +
                        "Cancellation remains valid.",
                        evt.AppointmentId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown — host cancellation token fired.
        }

        _logger.LogInformation("BookingCancelledEventHandler stopped.");
    }

    private async Task ProcessAsync(BookingCancelledEvent evt, CancellationToken ct)
    {
        using var scope     = _scopeFactory.CreateScope();
        var icsGenerator    = scope.ServiceProvider.GetRequiredService<IcsGenerator>();
        var artifactStorage = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
        var emailService    = scope.ServiceProvider.GetRequiredService<IConfirmationEmailService>();

        byte[]? icsBytes = null;
        string? icsPath  = null;

        // AC-4: generate cancellation ICS — non-blocking on failure.
        try
        {
            icsBytes = icsGenerator.GenerateCancellationIcs(
                appointmentId:      evt.AppointmentId,
                appointmentType:    evt.AppointmentType,
                providerName:       evt.ProviderName,
                confirmationCode:   evt.ConfirmationCode,
                originalStartTime:  evt.OriginalAppointmentTime,
                durationMinutes:    evt.DurationMinutes,
                location:           evt.Location,
                sequenceNumber:     evt.SequenceNumber);

            icsPath = await artifactStorage.StoreAsync(
                $"bookings/{evt.AppointmentId}",
                "cancellation.ics",
                icsBytes,
                "text/calendar",
                ct);

            _logger.LogInformation(
                "Cancellation ICS generated for appointment {AppointmentId} (SEQUENCE={Seq})",
                evt.AppointmentId, evt.SequenceNumber + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Cancellation ICS generation failed for appointment {AppointmentId}. " +
                "Cancellation email will be sent without ICS attachment.",
                evt.AppointmentId);
        }

        // Send cancellation confirmation email (with ICS bytes if available).
        await emailService.SendCancellationConfirmationAsync(evt, icsBytes, ct);

        _logger.LogInformation(
            "Cancellation notification dispatched for appointment {AppointmentId} [IcsStored={HasIcs}]",
            evt.AppointmentId, icsPath is not null);

        // AC-3 (US_026): Cancel all pending reminders for the cancelled appointment.
        // Failure is non-blocking — reminder cancellation must not affect committed cancellation.
        try
        {
            var reminderService = scope.ServiceProvider
                .GetRequiredService<IReminderSchedulingService>();

            await reminderService.CancelRemindersAsync(evt.AppointmentId, ct);

            _logger.LogInformation(
                "Pending reminders cancelled for appointment {AppointmentId}",
                evt.AppointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to cancel reminders for appointment {AppointmentId}. Cancellation remains valid.",
                evt.AppointmentId);
        }
    }
}

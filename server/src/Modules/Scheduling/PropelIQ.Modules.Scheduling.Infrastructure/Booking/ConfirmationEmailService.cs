using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts.Models;
using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Confirmation email delivery with Polly v8 retry (3 attempts, exponential backoff).
/// AC-2: email includes PDF, QR, and ICS attachments.
/// Edge case: booking persists regardless of email failure; all retries are audit-logged.
/// </summary>
public sealed class ConfirmationEmailService : IConfirmationEmailService
{
    private readonly ILogger<ConfirmationEmailService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public ConfirmationEmailService(ILogger<ConfirmationEmailService> logger)
    {
        _logger = logger;

        // Polly v8: 3 retries with exponential backoff (2^attempt seconds).
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts  = 3,
                BackoffType       = DelayBackoffType.Exponential,
                Delay             = TimeSpan.FromSeconds(1),
                UseJitter         = false,
                OnRetry           = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Email send attempt {AttemptNumber} failed. " +
                        "Retrying in {Delay:g}.",
                        args.AttemptNumber + 1,
                        args.RetryDelay);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task SendConfirmationAsync(
        BookingConfirmedEvent booking,
        ArtifactResult artifacts,
        CancellationToken ct)
    {
        await _retryPipeline.ExecuteAsync(async token =>
        {
            // AC-2: send confirmation email with PDF, QR, and ICS as attachments.
            // Placeholder implementation — inject IEmailSender (SendGrid / SMTP)
            // and call with ArtifactResult.PdfBytes, QrCodeBytes, IcsBytes.
            _logger.LogInformation(
                "Sending confirmation email for appointment {AppointmentId} " +
                "to {Email} [AllArtifacts={AllGenerated}]",
                booking.AppointmentId,
                booking.PatientEmail ?? "(no email)",
                artifacts.AllGenerated);

            // TODO: replace with real email provider call when IEmailSender is wired.
            await Task.CompletedTask;
        }, ct);
    }

    public async Task SendRescheduleConfirmationAsync(
        BookingRescheduledEvent booking,
        byte[]? icsBytes,
        CancellationToken ct)
    {
        await _retryPipeline.ExecuteAsync(async _ =>
        {
            _logger.LogInformation(
                "Sending reschedule confirmation email for appointment {AppointmentId} " +
                "to {Email} [HasUpdatedIcs={HasIcs}]",
                booking.AppointmentId,
                booking.PatientEmail ?? "(no email)",
                icsBytes is not null);

            // TODO: replace with real email provider call; attach icsBytes as appointment.ics.
            await Task.CompletedTask;
        }, ct);
    }

    public async Task SendCancellationConfirmationAsync(
        BookingCancelledEvent booking,
        byte[]? icsBytes,
        CancellationToken ct)
    {
        await _retryPipeline.ExecuteAsync(async _ =>
        {
            _logger.LogInformation(
                "Sending cancellation confirmation email for appointment {AppointmentId} " +
                "to {Email} [HasCancellationIcs={HasIcs}]",
                booking.AppointmentId,
                booking.PatientEmail ?? "(no email)",
                icsBytes is not null);

            // TODO: replace with real email provider call; attach icsBytes as cancellation.ics.
            await Task.CompletedTask;
        }, ct);
    }
}

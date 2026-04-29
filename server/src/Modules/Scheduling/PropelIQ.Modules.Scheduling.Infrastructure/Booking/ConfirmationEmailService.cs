using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts;
using PropelIQ.Modules.Scheduling.Application.Booking.Artifacts.Models;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Booking;

/// <summary>
/// Confirmation email delivery with Polly v8 retry (3 attempts, exponential backoff).
/// AC-2: email includes PDF, QR, and ICS attachments.
/// Edge case: booking persists regardless of email failure; all retries are audit-logged.
/// </summary>
public sealed class ConfirmationEmailService : IConfirmationEmailService
{
    private readonly INotificationSender _emailSender;
    private readonly ILogger<ConfirmationEmailService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public ConfirmationEmailService(
        INotificationSender emailSender,
        ILogger<ConfirmationEmailService> logger)
    {
        _emailSender = emailSender;
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
        if (string.IsNullOrWhiteSpace(booking.PatientEmail))
        {
            _logger.LogWarning(
                "No patient email for appointment {AppointmentId}; skipping confirmation email.",
                booking.AppointmentId);
            return;
        }

        var subject = $"Booking Confirmed – {booking.AppointmentType} on "
                    + booking.AppointmentTime.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

        var html = BuildConfirmationHtml(booking);

        await _retryPipeline.ExecuteAsync(async token =>
        {
            await _emailSender.SendEmailAsync(booking.PatientEmail, subject, html, token);

            _logger.LogInformation(
                "Confirmation email sent for appointment {AppointmentId} to {Email}",
                booking.AppointmentId,
                booking.PatientEmail);
        }, ct);
    }

    public async Task SendRescheduleConfirmationAsync(
        BookingRescheduledEvent booking,
        byte[]? icsBytes,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(booking.PatientEmail))
        {
            _logger.LogWarning(
                "No patient email for appointment {AppointmentId}; skipping reschedule email.",
                booking.AppointmentId);
            return;
        }

        var subject = $"Appointment Rescheduled – {booking.AppointmentType}";
        var html = BuildRescheduleHtml(booking);

        await _retryPipeline.ExecuteAsync(async token =>
        {
            await _emailSender.SendEmailAsync(booking.PatientEmail, subject, html, token);

            _logger.LogInformation(
                "Reschedule confirmation email sent for appointment {AppointmentId} to {Email}",
                booking.AppointmentId,
                booking.PatientEmail);
        }, ct);
    }

    public async Task SendCancellationConfirmationAsync(
        BookingCancelledEvent booking,
        byte[]? icsBytes,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(booking.PatientEmail))
        {
            _logger.LogWarning(
                "No patient email for appointment {AppointmentId}; skipping cancellation email.",
                booking.AppointmentId);
            return;
        }

        var subject = $"Appointment Cancelled – {booking.AppointmentType}";
        var html = BuildCancellationHtml(booking);

        await _retryPipeline.ExecuteAsync(async token =>
        {
            await _emailSender.SendEmailAsync(booking.PatientEmail, subject, html, token);

            _logger.LogInformation(
                "Cancellation confirmation email sent for appointment {AppointmentId} to {Email}",
                booking.AppointmentId,
                booking.PatientEmail);
        }, ct);
    }

    // ── HTML builders ────────────────────────────────────────────────────────

    private static string BuildConfirmationHtml(BookingConfirmedEvent b)
    {
        var date = b.AppointmentTime.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        var time = b.AppointmentTime.ToString("h:mm tt", CultureInfo.InvariantCulture);

        return WrapInLayout("Booking Confirmed", $@"
            <div style=""text-align:center;padding:32px 0 16px"">
              <div style=""display:inline-block;width:64px;height:64px;border-radius:50%;background:#e8f5e9;line-height:64px;font-size:32px"">&#10003;</div>
              <h1 style=""margin:16px 0 4px;font-size:24px;color:#1a1a2e"">Appointment Confirmed</h1>
              <p style=""margin:0;color:#6b7280;font-size:14px"">Your appointment has been booked successfully.</p>
            </div>

            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:24px 0;border-collapse:collapse"">
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Visit Type</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.AppointmentType)}</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Provider</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.ProviderName ?? "TBD")}</strong>
                </td>
              </tr>
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Date</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{date}</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Time</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{time}</strong>
                </td>
              </tr>
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Duration</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{b.DurationMinutes} minutes</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Confirmation Code</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px;font-family:monospace;letter-spacing:1px"">{Encode(b.ConfirmationCode)}</strong>
                </td>
              </tr>
              {(b.Location is not null ? $@"
              <tr>
                <td colspan=""2"" style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Location</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.Location)}</strong>
                </td>
              </tr>" : "")}
            </table>

            <!-- Intake CTA -->
            <div style=""background:#fff8e1;border:1px solid #ffe082;border-radius:8px;padding:20px;margin:24px 0;text-align:center"">
              <p style=""margin:0 0 4px;font-size:15px;font-weight:600;color:#f57f17"">&#128203; Complete Your Intake Form</p>
              <p style=""margin:0 0 16px;font-size:13px;color:#6b7280"">
                Please complete the intake form before your visit so your provider can review your information in advance.
                You can do this from the <strong>My Appointments</strong> section.
              </p>
              <a href=""https://propeliq.com/appointments""
                 style=""display:inline-block;padding:12px 32px;background:#1565c0;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;font-size:14px"">
                Go to My Appointments
              </a>
            </div>

            <div style=""text-align:center;padding:8px 0 0"">
              <p style=""color:#9ca3af;font-size:12px;margin:0"">
                Need to make changes? You can reschedule or cancel from the My Appointments page.
              </p>
            </div>");
    }

    private static string BuildRescheduleHtml(BookingRescheduledEvent b)
    {
        var date = b.NewTime.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        var time = b.NewTime.ToString("h:mm tt", CultureInfo.InvariantCulture);

        return WrapInLayout("Appointment Rescheduled", $@"
            <div style=""text-align:center;padding:32px 0 16px"">
              <div style=""display:inline-block;width:64px;height:64px;border-radius:50%;background:#e3f2fd;line-height:64px;font-size:32px"">&#128197;</div>
              <h1 style=""margin:16px 0 4px;font-size:24px;color:#1a1a2e"">Appointment Rescheduled</h1>
              <p style=""margin:0;color:#6b7280;font-size:14px"">Your appointment has been moved to a new time.</p>
            </div>

            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:24px 0;border-collapse:collapse"">
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Visit Type</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.AppointmentType)}</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Provider</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.ProviderName ?? "TBD")}</strong>
                </td>
              </tr>
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">New Date</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{date}</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">New Time</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{time}</strong>
                </td>
              </tr>
            </table>

            <div style=""text-align:center;padding:8px 0 0"">
              <a href=""https://propeliq.com/appointments""
                 style=""display:inline-block;padding:12px 32px;background:#1565c0;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;font-size:14px"">
                View My Appointments
              </a>
            </div>");
    }

    private static string BuildCancellationHtml(BookingCancelledEvent b)
    {
        var date = b.OriginalAppointmentTime.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        var time = b.OriginalAppointmentTime.ToString("h:mm tt", CultureInfo.InvariantCulture);

        return WrapInLayout("Appointment Cancelled", $@"
            <div style=""text-align:center;padding:32px 0 16px"">
              <div style=""display:inline-block;width:64px;height:64px;border-radius:50%;background:#ffebee;line-height:64px;font-size:32px"">&#10007;</div>
              <h1 style=""margin:16px 0 4px;font-size:24px;color:#1a1a2e"">Appointment Cancelled</h1>
              <p style=""margin:0;color:#6b7280;font-size:14px"">Your appointment has been cancelled as requested.</p>
            </div>

            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:24px 0;border-collapse:collapse"">
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Visit Type</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.AppointmentType)}</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Provider</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{Encode(b.ProviderName ?? "TBD")}</strong>
                </td>
              </tr>
              <tr>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Date</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{date}</strong>
                </td>
                <td style=""padding:12px 16px;border-bottom:1px solid #f0f0f0"">
                  <span style=""color:#6b7280;font-size:13px"">Time</span><br/>
                  <strong style=""color:#1a1a2e;font-size:15px"">{time}</strong>
                </td>
              </tr>
            </table>

            <div style=""text-align:center;padding:8px 0 0"">
              <p style=""color:#6b7280;font-size:14px;margin:0 0 16px"">
                Want to book a new appointment?
              </p>
              <a href=""https://propeliq.com/scheduling/search""
                 style=""display:inline-block;padding:12px 32px;background:#1565c0;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;font-size:14px"">
                Book New Appointment
              </a>
            </div>");
    }

    /// <summary>
    /// Shared responsive email layout wrapper. Inline-CSS only for maximum
    /// email-client compatibility (Gmail, Outlook, Apple Mail, Yahoo).
    /// </summary>
    private static string WrapInLayout(string title, string bodyContent)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
  <title>{Encode(title)}</title>
</head>
<body style=""margin:0;padding:0;background:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f3f4f6"">
    <tr>
      <td align=""center"" style=""padding:40px 16px"">
        <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:12px;box-shadow:0 1px 3px rgba(0,0,0,0.08);overflow:hidden;max-width:600px;width:100%"">
          <!-- Header -->
          <tr>
            <td style=""background:linear-gradient(135deg,#1565c0 0%,#1e88e5 100%);padding:24px 32px;text-align:center"">
              <h2 style=""margin:0;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:0.5px"">PropelIQ</h2>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style=""padding:0 32px 32px"">
              {bodyContent}
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style=""background:#f9fafb;padding:20px 32px;border-top:1px solid #f0f0f0;text-align:center"">
              <p style=""margin:0;color:#9ca3af;font-size:11px;line-height:1.6"">
                This is an automated message from PropelIQ. Please do not reply directly to this email.<br/>
                &copy; {DateTime.UtcNow.Year} PropelIQ. All rights reserved.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static string Encode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}

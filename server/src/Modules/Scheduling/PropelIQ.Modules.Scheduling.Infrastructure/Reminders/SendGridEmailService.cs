using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// AC-1: Sends formatted HTML reminder emails via SendGrid containing
/// appointment details (date, time, provider, location), a one-click
/// confirm button, and a one-click cancel link backed by HMAC-signed
/// token URLs from <see cref="IReminderTokenService"/>.
///
/// NFR-007: <see cref="SendGridOptions.ApiKey"/> is injected via
/// <c>IOptions</c> and never logged or included in exception messages.
/// </summary>
public sealed class SendGridEmailService : IReminderEmailService
{
    private readonly ISendGridClient _client;
    private readonly SendGridOptions _options;
    private readonly IReminderTokenService _tokenService;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(
        ISendGridClient client,
        IOptions<SendGridOptions> options,
        IReminderTokenService tokenService,
        ILogger<SendGridEmailService> logger)
    {
        _client       = client;
        _options       = options.Value;
        _tokenService = tokenService;
        _logger       = logger;
    }

    /// <inheritdoc/>
    public async Task SendReminderEmailAsync(
        ReminderEvent reminder,
        string recipientEmail,
        Appointment appointment,
        CancellationToken ct = default)
    {
        var confirmUrl = _tokenService.GenerateConfirmUrl(
            reminder.AppointmentId, reminder.Id);
        var cancelUrl = _tokenService.GenerateCancelUrl(
            reminder.AppointmentId, reminder.Id);

        var from = new EmailAddress(_options.FromAddress, _options.FromName);
        var to   = new EmailAddress(recipientEmail);

        var subject = $"Appointment Reminder — {appointment.ScheduledAt:MMMM dd, yyyy}";

        var htmlContent  = BuildReminderHtml(appointment, confirmUrl, cancelUrl);
        var plainContent = BuildReminderPlainText(appointment, confirmUrl, cancelUrl);

        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainContent, htmlContent);

        var response = await _client.SendEmailAsync(msg, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "SendGrid returned {StatusCode} for reminder {ReminderId}. " +
                "Response body logged at Debug level.",
                response.StatusCode, reminder.Id);
            _logger.LogDebug("SendGrid response body: {Body}", body);

            throw new HttpRequestException(
                $"SendGrid delivery failed with status {response.StatusCode}.");
        }

        _logger.LogInformation(
            "Reminder email sent via SendGrid for appointment {AppointmentId} " +
            "to {RecipientEmail}.",
            reminder.AppointmentId, recipientEmail);
    }

    /// <summary>
    /// AC-1: Builds HTML email with appointment details table,
    /// confirm button, and cancel link.
    /// </summary>
    private static string BuildReminderHtml(
        Appointment appt,
        string confirmUrl,
        string cancelUrl)
    {
        return $"""
        <div style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px;">
          <h2>Appointment Reminder</h2>
          <p>You have an upcoming appointment:</p>
          <table style="border-collapse:collapse;width:100%;">
            <tr>
              <td style="padding:8px;font-weight:bold;">Date</td>
              <td style="padding:8px;">{appt.ScheduledAt:dddd, MMMM dd, yyyy}</td>
            </tr>
            <tr>
              <td style="padding:8px;font-weight:bold;">Time</td>
              <td style="padding:8px;">{appt.ScheduledAt:hh:mm tt}</td>
            </tr>
            <tr>
              <td style="padding:8px;font-weight:bold;">Provider</td>
              <td style="padding:8px;">{appt.ProviderName ?? "TBD"}</td>
            </tr>
            <tr>
              <td style="padding:8px;font-weight:bold;">Location</td>
              <td style="padding:8px;">{appt.Location ?? "TBD"}</td>
            </tr>
          </table>
          <div style="margin-top:24px;text-align:center;">
            <a href="{confirmUrl}"
               style="display:inline-block;padding:12px 32px;background:#4CAF50;color:#fff;text-decoration:none;border-radius:4px;margin-right:16px;"
               role="button">Confirm Appointment</a>
            <a href="{cancelUrl}"
               style="display:inline-block;padding:12px 32px;background:#f44336;color:#fff;text-decoration:none;border-radius:4px;"
               role="button">Cancel Appointment</a>
          </div>
          <p style="margin-top:24px;font-size:12px;color:#666;">
            If you did not request this reminder, please ignore this email.</p>
        </div>
        """;
    }

    private static string BuildReminderPlainText(
        Appointment appt,
        string confirmUrl,
        string cancelUrl)
    {
        return $"""
        Appointment Reminder

        Date: {appt.ScheduledAt:dddd, MMMM dd, yyyy}
        Time: {appt.ScheduledAt:hh:mm tt}
        Provider: {appt.ProviderName ?? "TBD"}
        Location: {appt.Location ?? "TBD"}

        Confirm: {confirmUrl}
        Cancel: {cancelUrl}
        """;
    }
}

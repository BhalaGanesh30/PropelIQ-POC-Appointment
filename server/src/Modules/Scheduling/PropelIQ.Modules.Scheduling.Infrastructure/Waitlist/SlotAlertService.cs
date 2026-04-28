using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Application.Waitlist;
using PropelIQ.Modules.Scheduling.Application.Waitlist.Models;
using PropelIQ.Modules.Scheduling.Domain.Events;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

/// <summary>
/// Builds <see cref="SlotAlertPayload"/> and dispatches the preferred-slot
/// availability alert via each of the patient's enabled channels (US_030 AC-1).
///
/// NFR-007 / OWASP A02: HMAC-SHA256 claim tokens are derived from
/// <see cref="ReminderTokenOptions.HmacSecret"/>.  The secret is never logged.
/// Only a SHA-256 hash of the raw token is persisted on <c>WaitlistEntry</c>.
/// </summary>
public sealed class SlotAlertService : ISlotAlertService
{
    private readonly AppDbContext _db;
    private readonly ISendGridClient _sendGrid;
    private readonly SendGridOptions _sendGridOptions;
    private readonly ITwilioRestClient _twilio;
    private readonly TwilioOptions _twilioOptions;
    private readonly ReminderTokenOptions _tokenOptions;
    private readonly ILogger<SlotAlertService> _logger;

    public SlotAlertService(
        AppDbContext db,
        ISendGridClient sendGrid,
        IOptions<SendGridOptions> sendGridOptions,
        ITwilioRestClient twilio,
        IOptions<TwilioOptions> twilioOptions,
        IOptions<ReminderTokenOptions> tokenOptions,
        ILogger<SlotAlertService> logger)
    {
        _db              = db;
        _sendGrid        = sendGrid;
        _sendGridOptions  = sendGridOptions.Value;
        _twilio          = twilio;
        _twilioOptions   = twilioOptions.Value;
        _tokenOptions    = tokenOptions.Value;
        _logger          = logger;
    }

    /// <inheritdoc/>
    public async Task DispatchAlertAsync(SlotOfferedEvent evt, CancellationToken ct)
    {
        // ── Load patient contact data ─────────────────────────────────────────
        var patientData = await _db.Patients
            .Where(p => p.Id == evt.PatientId)
            .Select(p => new
            {
                FirstName        = p.FirstName,
                LastName         = p.LastName,
                Email            = p.User.Email,
                EmailEnabled     = p.ContactPreferences.EmailEnabled,
                SmsEnabled       = p.ContactPreferences.SmsEnabled,
                PreferredPhone   = p.ContactPreferences.PreferredPhone,
            })
            .FirstOrDefaultAsync(ct);

        if (patientData is null)
        {
            _logger.LogWarning(
                "SlotAlertService: patient {PatientId} not found — " +
                "skipping alert for waitlist entry {WaitlistEntryId}.",
                evt.PatientId, evt.WaitlistEntryId);
            return;
        }

        var patientName = $"{patientData.FirstName} {patientData.LastName}".Trim();

        // ── Generate HMAC claim token (AC-3 / OWASP A01) ─────────────────────
        var rawToken  = GenerateClaimToken(evt.WaitlistEntryId, evt.SlotId, evt.ClaimExpiresAt);
        var tokenHash = ComputeHash(rawToken);
        var claimUrl  = BuildClaimUrl(evt.WaitlistEntryId, rawToken);

        // Persist hash so the claim endpoint can validate without storing the secret.
        await _db.WaitlistEntries
            .Where(e => e.Id == evt.WaitlistEntryId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.ClaimTokenHash, tokenHash),
                ct);

        var payload = new SlotAlertPayload(
            WaitlistEntryId : evt.WaitlistEntryId,
            PatientId       : evt.PatientId,
            PatientName     : patientName,
            PatientEmail    : patientData.Email ?? string.Empty,
            PatientPhone    : patientData.PreferredPhone,
            SlotTime        : evt.SlotTime,
            AppointmentType : evt.AppointmentType,
            ProviderName    : evt.ProviderName,
            DurationMinutes : evt.DurationMinutes,
            ClaimUrl        : claimUrl,
            ExpiresAtUtc    : evt.ClaimExpiresAt);

        // ── Dispatch to enabled channels ──────────────────────────────────────
        if (patientData.EmailEnabled && !string.IsNullOrWhiteSpace(payload.PatientEmail))
        {
            await SendAlertEmailAsync(payload, ct);
        }

        if (patientData.SmsEnabled && !string.IsNullOrWhiteSpace(payload.PatientPhone))
        {
            await SendAlertSmsAsync(payload, ct);
        }
    }

    // ── Email dispatch ────────────────────────────────────────────────────────

    private async Task SendAlertEmailAsync(SlotAlertPayload payload, CancellationToken ct)
    {
        var from = new EmailAddress(_sendGridOptions.FromAddress, _sendGridOptions.FromName);
        var to   = new EmailAddress(payload.PatientEmail);

        var subject = $"A preferred slot is available — {payload.SlotTime:MMMM dd, yyyy}";

        var htmlContent  = BuildAlertHtml(payload);
        var plainContent = BuildAlertPlainText(payload);

        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainContent, htmlContent);
        var response = await _sendGrid.SendEmailAsync(msg, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "SendGrid returned {StatusCode} sending slot alert for " +
                "waitlist entry {WaitlistEntryId}. Response logged at Debug.",
                response.StatusCode, payload.WaitlistEntryId);
            _logger.LogDebug("SendGrid slot-alert response: {Body}", body);
            return;
        }

        _logger.LogInformation(
            "Slot alert email sent for waitlist entry {WaitlistEntryId} " +
            "to {Email}.",
            payload.WaitlistEntryId, payload.PatientEmail);
    }

    private static string BuildAlertHtml(SlotAlertPayload p)
    {
        var provider  = string.IsNullOrWhiteSpace(p.ProviderName) ? "your provider" : p.ProviderName;
        var expiresIn = (int)(p.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalHours;

        return $"""
            <p>Hi {p.PatientName},</p>
            <p>A preferred appointment slot has opened up that matches your waitlist request:</p>
            <ul>
              <li><strong>Date &amp; Time:</strong> {p.SlotTime:MMMM dd, yyyy 'at' hh:mm tt}</li>
              <li><strong>Type:</strong> {p.AppointmentType}</li>
              <li><strong>Provider:</strong> {provider}</li>
              <li><strong>Duration:</strong> {p.DurationMinutes} minutes</li>
            </ul>
            <p>
              <a href="{p.ClaimUrl}" style="...">Claim this slot</a>
            </p>
            <p>This offer expires in approximately {expiresIn} hour(s) ({p.ExpiresAtUtc:MMM dd, yyyy 'at' hh:mm tt} UTC).
               After that, the slot will be offered to the next patient on the waitlist.</p>
            <p>If you have questions, please contact your clinic directly.</p>
            """;
    }

    private static string BuildAlertPlainText(SlotAlertPayload p)
    {
        var provider  = string.IsNullOrWhiteSpace(p.ProviderName) ? "your provider" : p.ProviderName;
        var expiresIn = (int)(p.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalHours;

        return
            $"Hi {p.PatientName},\n\n" +
            $"A preferred slot is available:\n" +
            $"  Date & Time: {p.SlotTime:MMMM dd, yyyy 'at' hh:mm tt}\n" +
            $"  Type:        {p.AppointmentType}\n" +
            $"  Provider:    {provider}\n" +
            $"  Duration:    {p.DurationMinutes} minutes\n\n" +
            $"Claim it here (offer expires in ~{expiresIn}h):\n{p.ClaimUrl}\n\n" +
            $"Reply STOP to opt out of slot alerts.";
    }

    // ── SMS dispatch ──────────────────────────────────────────────────────────

    private async Task SendAlertSmsAsync(SlotAlertPayload payload, CancellationToken ct)
    {
        var provider  = string.IsNullOrWhiteSpace(payload.ProviderName) ? "" : $" w/ {payload.ProviderName}";
        var body =
            $"PropelIQ: A preferred {payload.AppointmentType}{provider} slot opened on " +
            $"{payload.SlotTime:MMM dd 'at' hh:mm tt}. " +
            $"Claim by {payload.ExpiresAtUtc:hh:mm tt} UTC: {payload.ClaimUrl}";

        try
        {
            var message = await MessageResource.CreateAsync(
                to:     new PhoneNumber(payload.PatientPhone),
                from:   new PhoneNumber(_twilioOptions.FromNumber),
                body:   body,
                client: _twilio);

            if (message.ErrorCode is not null)
            {
                _logger.LogWarning(
                    "Twilio error {ErrorCode} sending slot alert for " +
                    "waitlist entry {WaitlistEntryId}: {ErrorMessage}.",
                    message.ErrorCode, payload.WaitlistEntryId, message.ErrorMessage);
                return;
            }

            _logger.LogInformation(
                "Slot alert SMS sent for waitlist entry {WaitlistEntryId} " +
                "to {Phone}.",
                payload.WaitlistEntryId, payload.PatientPhone);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Twilio exception sending slot alert for waitlist entry {WaitlistEntryId}.",
                payload.WaitlistEntryId);
        }
    }

    // ── Token generation helpers ──────────────────────────────────────────────

    /// <summary>
    /// Generates an HMAC-SHA256-signed claim token encoding the waitlist entry ID,
    /// slot ID, and expiry timestamp.
    ///
    /// Format: entryId(16) + slotId(16) + expiresAtTicks(8) = 40 bytes payload
    ///         + HMAC-SHA256(32) = 72 bytes total → base64url string.
    /// </summary>
    private string GenerateClaimToken(
        Guid entryId, Guid slotId, DateTimeOffset expiresAt)
    {
        Span<byte> payload = stackalloc byte[40];
        entryId.TryWriteBytes(payload[..16]);
        slotId.TryWriteBytes(payload[16..32]);
        BitConverter.TryWriteBytes(payload[32..40], expiresAt.UtcTicks);

        var secretBytes = GetSecretBytes();
        using var hmac  = new HMACSHA256(secretBytes);
        var hmacBytes   = hmac.ComputeHash(payload.ToArray());

        var token = new byte[72];
        payload.CopyTo(token);
        hmacBytes.CopyTo(token, 40);

        return Convert.ToBase64String(token)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Returns a SHA-256 hex digest of the raw token for storage.
    /// The stored hash is compared (timing-safe) on claim to prevent forgery.
    /// </summary>
    private static string ComputeHash(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private string BuildClaimUrl(Guid entryId, string rawToken)
        => $"{_tokenOptions.BaseUrl}/waitlist/{entryId}/claim?token={rawToken}";

    private byte[] GetSecretBytes()
    {
        try
        {
            return Convert.FromBase64String(_tokenOptions.HmacSecret);
        }
        catch
        {
            // If the secret is not base64 (e.g., raw string in dev), encode it directly.
            return Encoding.UTF8.GetBytes(_tokenOptions.HmacSecret);
        }
    }
}

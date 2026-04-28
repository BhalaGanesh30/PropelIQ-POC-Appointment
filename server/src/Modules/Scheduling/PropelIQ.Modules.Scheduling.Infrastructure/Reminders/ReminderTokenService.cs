using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Scheduling.Application.Reminders;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// HMAC-SHA256 signed token service for one-click appointment confirmation
/// and cancellation links embedded in reminder emails and SMS.
///
/// Token format (binary, base64url-encoded):
///   AppointmentId (16 bytes) + ReminderId (16 bytes) + ActionByte (1 byte) + HMAC-SHA256 (32 bytes)
///   Total: 65 bytes → ~88 chars base64url.
///
/// Security:
/// - <see cref="CryptographicOperations.FixedTimeEquals"/> prevents timing attacks.
/// - HMAC secret from <see cref="ReminderTokenOptions.HmacSecret"/> is never logged.
/// - Expiry is validated server-side against <c>Appointment.ScheduledAt</c>,
///   not embedded in the token, to avoid token length bloat for SMS.
/// </summary>
public sealed class ReminderTokenService : IReminderTokenService
{
    private readonly ReminderTokenOptions _options;

    public ReminderTokenService(IOptions<ReminderTokenOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public string GenerateConfirmUrl(Guid appointmentId, Guid reminderId)
    {
        var token = BuildToken(appointmentId, reminderId, ActionByte.Confirm);
        return $"{_options.BaseUrl}/api/v1/reminders/confirm?token={token}";
    }

    /// <inheritdoc/>
    public string GenerateCancelUrl(Guid appointmentId, Guid reminderId)
    {
        var token = BuildToken(appointmentId, reminderId, ActionByte.Cancel);
        return $"{_options.BaseUrl}/api/v1/reminders/cancel?token={token}";
    }

    /// <inheritdoc/>
    public string GenerateActionUrl(Guid appointmentId, Guid reminderId)
    {
        var token = BuildToken(appointmentId, reminderId, ActionByte.Action);
        return $"{_options.BaseUrl}/api/v1/reminders/action?token={token}";
    }

    /// <inheritdoc/>
    public ReminderTokenPayload? ValidateToken(string token)
    {
        try
        {
            // Re-pad base64url → standard base64.
            var base64 = token.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var decoded = Convert.FromBase64String(base64);

            // Payload: AppointmentId(16) + ReminderId(16) + ActionByte(1) = 33
            // HMAC-SHA256: 32 bytes → total = 65 bytes minimum.
            const int payloadLength = 33;
            const int hmacLength = 32;
            if (decoded.Length != payloadLength + hmacLength)
                return null;

            var payload      = decoded.AsSpan(0, payloadLength);
            var receivedHmac = decoded.AsSpan(payloadLength, hmacLength);

            using var hmac = new HMACSHA256(GetSecretBytes());
            var computedHmac = hmac.ComputeHash(payload.ToArray());

            if (!CryptographicOperations.FixedTimeEquals(receivedHmac, computedHmac))
                return null;

            var appointmentId = new Guid(payload[..16]);
            var reminderId    = new Guid(payload[16..32]);
            var actionByte    = payload[32];

            var action = actionByte switch
            {
                (byte)ActionByte.Confirm => "confirm",
                (byte)ActionByte.Cancel  => "cancel",
                (byte)ActionByte.Action  => "action",
                _ => null
            };

            if (action is null)
                return null;

            return new ReminderTokenPayload(appointmentId, reminderId, action);
        }
        catch
        {
            // Malformed base64, wrong length, etc. — treat as invalid.
            return null;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string BuildToken(Guid appointmentId, Guid reminderId, ActionByte action)
    {
        var payload = new byte[33];
        appointmentId.ToByteArray().CopyTo(payload, 0);
        reminderId.ToByteArray().CopyTo(payload, 16);
        payload[32] = (byte)action;

        using var hmac = new HMACSHA256(GetSecretBytes());
        var signature = hmac.ComputeHash(payload);

        var tokenBytes = new byte[payload.Length + signature.Length];
        payload.CopyTo(tokenBytes, 0);
        signature.CopyTo(tokenBytes, payload.Length);

        return Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private byte[] GetSecretBytes()
    {
        // Accept base64-encoded or raw UTF-8 secret.
        try { return Convert.FromBase64String(_options.HmacSecret); }
        catch { return System.Text.Encoding.UTF8.GetBytes(_options.HmacSecret); }
    }

    private enum ActionByte : byte
    {
        Confirm = 0,
        Cancel  = 1,
        Action  = 2
    }
}

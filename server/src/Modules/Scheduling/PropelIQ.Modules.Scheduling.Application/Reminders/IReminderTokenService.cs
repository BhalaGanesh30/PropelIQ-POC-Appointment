namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Generates and validates HMAC-SHA256 signed tokens embedded in reminder
/// email/SMS links for one-click appointment confirmation and cancellation.
///
/// Each token encodes <c>AppointmentId</c>, <c>ReminderId</c>, an action type,
/// and an expiry timestamp (set to appointment start time).
/// Validation uses <c>CryptographicOperations.FixedTimeEquals</c> for
/// timing-attack resistance.
/// </summary>
public interface IReminderTokenService
{
    /// <summary>Generates a signed one-click confirm URL for the appointment.</summary>
    string GenerateConfirmUrl(Guid appointmentId, Guid reminderId);

    /// <summary>Generates a signed one-click cancel URL for the appointment.</summary>
    string GenerateCancelUrl(Guid appointmentId, Guid reminderId);

    /// <summary>Generates a combined action short-link (for SMS — single URL).</summary>
    string GenerateActionUrl(Guid appointmentId, Guid reminderId);

    /// <summary>
    /// Validates an HMAC-signed token and extracts the payload.
    /// Returns <c>null</c> if the token is invalid, tampered, or malformed.
    /// </summary>
    ReminderTokenPayload? ValidateToken(string token);
}

/// <summary>
/// Decoded payload from a validated HMAC-signed reminder action token.
/// <c>ExpiresAt</c> is not embedded in the token; expiry is checked server-side
/// against the appointment's <c>ScheduledAt</c> value.
/// </summary>
public sealed record ReminderTokenPayload(
    Guid AppointmentId,
    Guid ReminderId,
    string Action);

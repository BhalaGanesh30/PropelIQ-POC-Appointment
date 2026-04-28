namespace PropelIQ.Modules.Scheduling.Application.Waitlist.Models;

/// <summary>
/// Fully-resolved content payload for a preferred-slot availability alert (US_030 AC-1).
/// Built by <see cref="ISlotAlertService"/> before multi-channel dispatch.
///
/// Does NOT contain PII beyond what is strictly needed for the notification.
/// Fields are populated from the local database — no external AI service involvement.
/// </summary>
public sealed record SlotAlertPayload(
    Guid WaitlistEntryId,
    Guid PatientId,
    string PatientName,
    string PatientEmail,
    string? PatientPhone,
    DateTimeOffset SlotTime,
    string AppointmentType,
    string? ProviderName,
    int DurationMinutes,
    /// <summary>HMAC-signed claim URL embedded in email/SMS (safe to send externally).</summary>
    string ClaimUrl,
    /// <summary>UTC deadline by which the patient must click "Claim" (edge case 2).</summary>
    DateTimeOffset ExpiresAtUtc);

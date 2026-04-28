namespace PropelIQ.Modules.Scheduling.Application.Reminders.Models;

/// <summary>
/// Inbound payload for PUT /api/v1/patients/me/notification-preferences (AC-1, AC-2).
/// </summary>
/// <param name="EmailEnabled">Whether email reminders are active for this patient.</param>
/// <param name="SmsEnabled">Whether SMS reminders are active for this patient.</param>
/// <param name="ReminderTimings">
/// Subset of allowed offset keys: "7d", "2d", "1d", "2h".
/// Only the selected keys produce a ReminderEvent at scheduling time.
/// </param>
public sealed record NotificationPreferenceDto(
    bool EmailEnabled,
    bool SmsEnabled,
    IReadOnlyList<string> ReminderTimings);

/// <summary>
/// Response shape for both GET and PUT preference endpoints.
/// Includes <see cref="HasPhoneNumber"/> so the frontend can prompt the patient
/// to add a verified mobile number before enabling SMS (edge case 1).
/// </summary>
/// <param name="EmailEnabled">Current email channel state.</param>
/// <param name="SmsEnabled">Current SMS channel state.</param>
/// <param name="ReminderTimings">Currently active offset keys.</param>
/// <param name="HasPhoneNumber">
/// True when a non-empty <c>PreferredPhone</c> is stored for this patient.
/// Required to enable SMS reminders.
/// </param>
public sealed record NotificationPreferenceResponse(
    bool EmailEnabled,
    bool SmsEnabled,
    IReadOnlyList<string> ReminderTimings,
    bool HasPhoneNumber);

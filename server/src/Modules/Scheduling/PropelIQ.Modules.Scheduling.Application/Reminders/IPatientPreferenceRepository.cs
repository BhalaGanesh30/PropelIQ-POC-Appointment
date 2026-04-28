using PropelIQ.Modules.Scheduling.Application.Reminders.Models;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Reads and writes patient contact channel preferences to determine which channels
/// (Email, SMS, or both) reminders should be created for.
/// Implemented in the Infrastructure layer.
/// </summary>
public interface IPatientPreferenceRepository
{
    /// <summary>
    /// Returns the patient's full notification preferences including channel
    /// flags, reminder timings, and whether a phone number is on file.
    /// </summary>
    Task<NotificationPreferenceResponse> GetPreferencesAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// Persists updated notification preferences for the patient.
    /// Only affects future reminder scheduling — same-day reminders already
    /// in Pending or Sending status are not modified (edge case 2).
    /// </summary>
    Task SavePreferencesAsync(
        Guid patientId,
        NotificationPreferenceDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the enabled notification channel names ("Email", "Sms", or both)
    /// for the given patient, based on their stored ContactPreferences.
    /// Returns a default of ["Email"] if the patient record is not found.
    /// AC-4: returns an empty list when all channels are disabled so the
    /// dispatch worker records the event as OptedOut.
    /// </summary>
    Task<IReadOnlyList<string>> GetEnabledChannelsAsync(
        Guid patientId,
        CancellationToken ct = default);
}


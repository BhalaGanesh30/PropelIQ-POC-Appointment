namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Reads patient contact channel preferences to determine which channels
/// (Email, SMS, or both) reminders should be created for.
/// Implemented in the Infrastructure layer.
/// </summary>
public interface IPatientPreferenceRepository
{
    /// <summary>
    /// Returns the enabled notification channel names ("Email", "Sms", or both)
    /// for the given patient, based on their stored ContactPreferences.
    /// Returns a default of ["Email"] if the patient record is not found.
    /// </summary>
    Task<IReadOnlyList<string>> GetEnabledChannelsAsync(
        Guid patientId,
        CancellationToken ct = default);
}

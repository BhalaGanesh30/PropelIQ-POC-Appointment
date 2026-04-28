namespace PropelIQ.Modules.Scheduling.Application.AI;

/// <summary>
/// Extracts aggregated patient history features used to build the no-show risk
/// scoring prompt. Only non-PII aggregated counts and appointment metadata are
/// returned — no patient names, contact details, or identifiers (AIR-009).
/// </summary>
public interface IPatientHistoryFeatureExtractor
{
    /// <summary>
    /// Returns aggregated history features for a patient, used to construct the
    /// risk scoring prompt without exposing PII to the AI gateway (AIR-009).
    /// </summary>
    Task<PatientHistoryFeatures> ExtractAsync(
        Guid patientId,
        CancellationToken ct = default);
}

/// <summary>
/// Aggregated, PII-free patient history features used in the risk scoring prompt.
/// </summary>
public sealed record PatientHistoryFeatures(
    int TotalAppointments,
    int NoShowCount,
    int CancellationCount,
    int ConfirmedViaReminderCount,
    double AverageLeadTimeDays,
    string DayOfWeek,
    string TimeOfDay);

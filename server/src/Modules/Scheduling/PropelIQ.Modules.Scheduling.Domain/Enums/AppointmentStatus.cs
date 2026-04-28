namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// Lifecycle status of a booked appointment.
/// Stored as a string in the database (see AppointmentConfiguration).
/// </summary>
public enum AppointmentStatus
{
    Confirmed = 0,
    Cancelled = 1,
    Completed = 2,
    NoShow    = 3,
    /// <summary>
    /// AC-3 (US_027): Patient explicitly confirmed the appointment
    /// via one-click confirm link in reminder email/SMS.
    /// </summary>
    PatientConfirmed = 4,
}

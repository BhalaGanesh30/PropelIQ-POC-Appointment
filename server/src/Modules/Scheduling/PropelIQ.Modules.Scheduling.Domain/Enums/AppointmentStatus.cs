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
}

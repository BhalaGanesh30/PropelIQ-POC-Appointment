namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// Real-time queue state for an appointment on the day of visit (EP-004 US_031/US_032).
/// Stored as a string on <see cref="Domain.Entities.Appointment.QueueState"/>.
///
/// String storage: use <c>.ToString()</c> when writing to the Appointment entity and
/// <c>Enum.Parse&lt;QueueState&gt;()</c> when reading for state machine validation.
/// </summary>
public enum QueueState
{
    /// <summary>Patient has not yet arrived; appointment is scheduled for today (US_032 AC-1).</summary>
    Scheduled = 0,

    /// <summary>
    /// Legacy alias — pre-US_032 rows where QueueState was set to "Waiting".
    /// Treated as equivalent to <see cref="Arrived"/> in the state machine.
    /// </summary>
    Waiting = 1,

    /// <summary>Patient has checked in and is waiting to be seen (US_032 AC-1 → AC-2).</summary>
    Arrived = 2,

    /// <summary>Patient has been called in; visit is underway (US_032 AC-2 → AC-3).</summary>
    InProgress = 3,

    /// <summary>Visit has ended successfully (US_032 AC-3).</summary>
    Completed = 4,

    /// <summary>Patient did not attend and no prior cancellation was received (US_032 AC-4).</summary>
    NoShow = 5,
}

namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// Machine-readable types of scheduling constraints that can be overridden by
/// privileged staff (EP-004 US_034 FR-SO-004).
///
/// Each value maps to a distinct check in <c>SchedulingOverrideService</c> that
/// validates the constraint is actually violated for the target appointment before
/// allowing the override — preventing fabricated override payloads (AC-2).
/// </summary>
public enum SchedulingConstraintType
{
    /// <summary>
    /// Cancellation attempted within 24 hours of the scheduled appointment time.
    /// </summary>
    CancellationWithin24Hours = 0,

    /// <summary>
    /// Reschedule attempted within 24 hours of the current scheduled appointment time.
    /// </summary>
    RescheduleWithin24Hours = 1,

    /// <summary>
    /// The target slot is already claimed by another booking (concurrent booking conflict).
    /// </summary>
    SlotConflict = 2,

    /// <summary>
    /// The walk-in queue has reached the configured capacity threshold.
    /// </summary>
    CapacityExceeded = 3,
}

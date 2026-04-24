namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// Lifecycle status of a waitlist entry (US_023).
///
/// Active    — waiting for a matching slot to become available (AC-1).
/// Offered   — a matching slot was found; claim window is open (AC-2).
/// Claimed   — patient successfully reserved the offered slot (AC-3).
/// Expired   — patient did not claim within 2 hours; slot rotated to next (AC-4).
/// Cancelled — patient or staff explicitly removed the entry.
/// </summary>
public enum WaitlistStatus
{
    Active    = 0,
    Offered   = 1,
    Claimed   = 2,
    Expired   = 3,
    Cancelled = 4,
}

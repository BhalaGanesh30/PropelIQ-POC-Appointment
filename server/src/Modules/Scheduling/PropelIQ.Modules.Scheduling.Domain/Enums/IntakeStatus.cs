namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// Lifecycle states for an intake draft (AC-3 resume, edge case: 7-day retention).
/// </summary>
public enum IntakeStatus
{
    /// <summary>Partial form data saved — patient has not yet submitted.</summary>
    Draft = 0,

    /// <summary>Form submitted and attached to the appointment booking (AC-4).</summary>
    Submitted = 1,

    /// <summary>Draft was not submitted within 7 days and was expired by cleanup service.</summary>
    Expired = 2,
}

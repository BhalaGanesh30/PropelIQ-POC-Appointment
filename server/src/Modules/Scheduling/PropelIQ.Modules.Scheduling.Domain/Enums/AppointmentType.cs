namespace PropelIQ.Modules.Scheduling.Domain.Enums;

public enum AppointmentType
{
    General = 0,
    Specialist = 1,
    FollowUp = 2,
    Urgent = 3,

    /// <summary>Walk-in patient without a prior booking (EP-004 US_033).</summary>
    WalkIn = 4,
}

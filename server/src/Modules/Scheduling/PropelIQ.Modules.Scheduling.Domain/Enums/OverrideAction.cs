namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// The scheduling action that is being override-executed by a privileged staff member
/// (EP-004 US_034 FR-SO-004).
/// </summary>
public enum OverrideAction
{
    /// <summary>Cancel an appointment despite an active scheduling constraint.</summary>
    Cancel = 0,

    /// <summary>Reschedule an appointment despite an active scheduling constraint.</summary>
    Reschedule = 1,

    /// <summary>Force-book a slot that is blocked by a conflict or capacity constraint.</summary>
    ForceBook = 2,
}

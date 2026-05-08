namespace PropelIQ.Modules.Scheduling.Domain.Enums;

/// <summary>
/// Verbs passed to <c>IAppointmentStateMachineService.TransitionAsync</c>
/// to drive the US_032 check-in state machine.
/// </summary>
public enum AppointmentStateAction
{
    /// <summary>AC-1: Record patient arrival; transitions Scheduled → Arrived.</summary>
    CheckIn = 0,

    /// <summary>AC-2: Clinician calls patient in; transitions Arrived → InProgress.</summary>
    StartVisit = 1,

    /// <summary>AC-3: Visit complete; transitions InProgress → Completed.</summary>
    CompleteVisit = 2,

    /// <summary>AC-4: Patient did not attend; transitions Scheduled|Arrived → NoShow.</summary>
    NoShow = 3,
}

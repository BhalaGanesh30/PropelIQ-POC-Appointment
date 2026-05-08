using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Queue.Dto;

/// <summary>
/// A single patient entry in the real-time queue response (EP-004 US_031).
///
/// AC-1: Carries status badge, wait-time estimate, and appointment metadata.
/// AC-3: <see cref="IsOverdue"/> is true when the patient has waited longer
///       than <see cref="EstimatedWaitMinutes"/>.
/// </summary>
public sealed record QueueEntryDto
{
    public required Guid AppointmentId { get; init; }
    public required Guid PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string AppointmentType { get; init; }
    public required QueueState Status { get; init; }

    /// <summary>
    /// UTC timestamp when the patient arrived for check-in.
    /// Null until task_004 migration adds the ArrivedAt column and a check-in
    /// workflow sets this field.  Defaults to ScheduledAt as a fallback.
    /// </summary>
    public required DateTimeOffset? ArrivedAt { get; init; }

    /// <summary>ISO-8601 UTC timestamp of the scheduled appointment start.</summary>
    public required DateTimeOffset ScheduledAt { get; init; }

    /// <summary>Server-computed estimated wait in minutes (from IWaitTimeEstimationService).</summary>
    public required int EstimatedWaitMinutes { get; init; }

    /// <summary>
    /// Elapsed minutes since arrival (or ScheduledAt when ArrivedAt is absent).
    /// </summary>
    public required int ActualWaitMinutes { get; init; }

    /// <summary>
    /// AC-3: True when <see cref="ActualWaitMinutes"/> exceeds <see cref="EstimatedWaitMinutes"/>.
    /// Drives the overdue row highlight in the queue dashboard (UXR-303).
    /// </summary>
    public required bool IsOverdue { get; init; }

    /// <summary>
    /// AC-3 (US_033): True when the appointment was created as a walk-in entry
    /// (AppointmentType == WalkIn). Drives the "Walk-In" badge on the dashboard.
    /// Non-required — defaults to false for all existing callers that do not set it.
    /// </summary>
    public bool IsWalkIn { get; init; }
}

namespace PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

/// <summary>
/// Response body for POST /api/v1/walkins (EP-004 US_033 AC-1, Edge Case 2).
///
/// AC-1: Includes queue position and estimated wait time so the front desk
///       can communicate expected wait to the walk-in patient.
/// Edge Case 2: AtCapacity flag warns the caller when today's queue has
///              reached the configured WalkIn:CapacityThreshold.
/// </summary>
public sealed class WalkinResponse
{
    /// <summary>PK of the newly created WalkIn record.</summary>
    public required Guid WalkinId { get; init; }

    /// <summary>PK of the Appointment queue entry created for this walk-in.</summary>
    public required Guid AppointmentId { get; init; }

    /// <summary>Patient display name.</summary>
    public required string PatientName { get; init; }

    /// <summary>Visit reason as provided by staff.</summary>
    public required string VisitReason { get; init; }

    /// <summary>1-based position in today's active queue at time of creation.</summary>
    public required int QueuePosition { get; init; }

    /// <summary>Server-computed estimated wait in minutes at time of creation.</summary>
    public required int EstimatedWaitMinutes { get; init; }

    /// <summary>
    /// Edge Case 2: True when today's total appointment count meets or exceeds
    /// the WalkIn:CapacityThreshold configuration value (default 50).
    /// Walk-in creation is still permitted but the UI should surface the warning.
    /// </summary>
    public required bool AtCapacity { get; init; }

    /// <summary>
    /// Populated when a new or existing patient account is linked to this walk-in.
    /// Null for fully anonymous walk-ins that have not been converted.
    /// </summary>
    public Guid? PatientId { get; init; }
}

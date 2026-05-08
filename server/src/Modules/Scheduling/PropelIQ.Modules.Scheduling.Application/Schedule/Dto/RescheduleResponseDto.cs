namespace PropelIQ.Modules.Scheduling.Application.Schedule.Dto;

/// <summary>
/// Response returned by <c>PUT /api/v1/schedule/reschedule</c> on success (AC-2).
/// </summary>
public sealed class RescheduleResponseDto
{
    public required Guid AppointmentId { get; init; }
    /// <summary>UTC start time before the reschedule (for client-side rollback / display).</summary>
    public required DateTimeOffset OldStartTime { get; init; }
    /// <summary>UTC start time after the reschedule (reflects what was persisted).</summary>
    public required DateTimeOffset NewStartTime { get; init; }
    /// <summary>UUID of the immutable audit record created by this operation (AC-2, NFR-010).</summary>
    public required Guid AuditRecordId { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Scheduling.Application.Schedule.Dto;

/// <summary>
/// Request body for <c>PUT /api/v1/schedule/reschedule</c> (AC-2).
///
/// <see cref="OverrideReason"/> is mandatory — collected by the FE via
/// <c>OverrideReasonDialogComponent</c> before the API call is made (US_034 integration).
/// </summary>
public sealed class RescheduleRequestDto
{
    [Required]
    public required Guid AppointmentId { get; init; }

    /// <summary>Requested new start time in UTC (ISO-8601).</summary>
    [Required]
    public required DateTimeOffset NewStartTime { get; init; }

    /// <summary>
    /// Mandatory override reason justifying the rescheduling action (AC-2).
    /// Persisted verbatim in the immutable audit record (NFR-010).
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public required string OverrideReason { get; init; }
}

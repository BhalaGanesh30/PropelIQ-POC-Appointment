using System.ComponentModel.DataAnnotations;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Override.Dto;

/// <summary>
/// Request body for <c>POST /api/v1/scheduling/override</c> (EP-004 US_034).
///
/// AC-2: Reason is mandatory and stored verbatim in the immutable AuditRecord.
/// AC-3: Empty / whitespace-only reason is rejected — <c>[MinLength(1)]</c> after trim.
/// Edge Case 1: Reason exceeding 500 characters is rejected with a 400 response.
/// Edge Case 2: Only Staff and Admin callers reach this DTO; Patient role
///              is blocked at the controller via <c>[Authorize(Roles="Staff,Admin")]</c>.
/// </summary>
public sealed class SchedulingOverrideRequest
{
    /// <summary>UUID of the appointment whose constraint is being overridden.</summary>
    [Required]
    public Guid AppointmentId { get; init; }

    /// <summary>
    /// Machine-readable type of the violated scheduling constraint.
    /// The service validates the constraint actually applies to the appointment.
    /// </summary>
    [Required]
    public SchedulingConstraintType ConstraintType { get; init; }

    /// <summary>
    /// Staff-provided justification (1–500 characters, trimmed).
    /// Stored verbatim in the AuditRecord — immutable per NFR-010.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "Override reason is required.")]
    [MaxLength(500, ErrorMessage = "Override reason must be 500 characters or fewer.")]
    public required string Reason { get; init; }

    /// <summary>The scheduling action to execute by bypassing the constraint.</summary>
    [Required]
    public OverrideAction Action { get; init; }
}

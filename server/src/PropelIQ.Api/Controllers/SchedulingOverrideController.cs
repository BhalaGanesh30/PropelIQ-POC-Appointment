using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Override.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Scheduling override API (EP-004 US_034 FR-SO-004).
///
/// POST /api/v1/scheduling/override — validates the scheduling constraint,
/// executes the requested override action, and writes an immutable audit record.
///
/// AC-1: Returns 422 when the constraint does not actually apply, preventing
///       fabricated override payloads.
/// AC-2: On success returns overrideId + auditRecordId for audit correlation.
/// AC-3: Empty / whitespace-only reason → 400 (DTO validation via [MinLength(1)]).
/// Edge Case 1: Reason > 500 chars → 400 (DTO validation via [MaxLength(500)]).
/// Edge Case 2: Patient role → 403 (enforced by [Authorize(Roles="Staff,Admin")]).
///
/// NFR-010: Every override writes an immutable AuditRecord.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[Route("api/v1/scheduling")]
[ApiController]
[Produces("application/json")]
public sealed class SchedulingOverrideController : BaseApiController
{
    private readonly ISchedulingOverrideService _overrideService;

    public SchedulingOverrideController(ISchedulingOverrideService overrideService)
    {
        _overrideService = overrideService;
    }

    /// <summary>
    /// Applies a scheduling override — validates the constraint, executes the action,
    /// and writes an immutable audit record within a single database transaction.
    /// </summary>
    /// <param name="request">Override payload: appointmentId, constraintType, reason (1–500 chars), action.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Override ID and audit record ID for client-side correlation.</returns>
    /// <response code="200">Override applied; overrideId and auditRecordId returned.</response>
    /// <response code="400">Validation failure — empty reason, reason too long, or invalid enum values.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role (Patient → 403).</response>
    /// <response code="404">Appointment not found.</response>
    /// <response code="422">Stated constraint does not apply to the appointment (fabrication guard).</response>
    [HttpPost("override")]
    [ProducesResponseType(typeof(SchedulingOverrideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExecuteOverride(
        [FromBody] SchedulingOverrideRequest request,
        CancellationToken ct)
    {
        var staffUserId = TryGetCurrentUserId();
        if (staffUserId is null)
            return Unauthorized(new { message = "Staff identity could not be resolved from token." });

        try
        {
            var result = await _overrideService.ExecuteOverrideAsync(request, staffUserId.Value, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // AC-1: Constraint does not apply to the appointment.
            return UnprocessableEntity(new { message = ex.Message });
        }
    }
}

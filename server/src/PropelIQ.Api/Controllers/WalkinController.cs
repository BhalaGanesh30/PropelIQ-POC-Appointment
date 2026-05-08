using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Walk-in registration and conversion API (EP-004 US_033).
///
/// POST /api/v1/walkins       — AC-1/AC-2/AC-4: Create walk-in; optional inline
///                              patient registration or existing-patient linking.
/// POST /api/v1/walkins/{id}/convert — AC-2: Convert anonymous walk-in to a
///                                     full patient account.
///
/// FR-SO-003: Restricted to Staff and Admin roles.
/// NFR-010: Every operation writes an immutable AuditRecord.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[Route("api/v1/walkins")]
[ApiController]
[Produces("application/json")]
public sealed class WalkinController : BaseApiController
{
    private readonly IWalkinService _walkinService;

    public WalkinController(IWalkinService walkinService)
    {
        _walkinService = walkinService;
    }

    /// <summary>
    /// Creates a walk-in record and inserts the patient into today's queue.
    /// </summary>
    /// <remarks>
    /// AC-1: Minimum payload is PatientName + VisitReason.
    /// AC-2: Set ConvertToPatient=true with DateOfBirth and Email to register inline.
    /// AC-4: Provide ExistingPatientId (from GET /api/v1/patients/search) to link to
    ///       an existing account without duplication.
    /// Edge Case 2: Response AtCapacity=true when queue ≥ WalkIn:CapacityThreshold.
    /// </remarks>
    /// <param name="request">Walk-in creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Queue position, estimated wait, and capacity flag.</returns>
    /// <response code="201">Walk-in created; queue entry inserted.</response>
    /// <response code="400">Validation failure (missing required fields or invalid format).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    /// <response code="404">ExistingPatientId was provided but no matching patient found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WalkinResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateWalkin(
        [FromBody] CreateWalkinRequest request,
        CancellationToken ct)
    {
        var staffUserId = TryGetCurrentUserId();
        if (staffUserId is null)
            return Unauthorized(new { message = "Staff identity could not be resolved from token." });

        try
        {
            var response = await _walkinService.CreateWalkinAsync(request, staffUserId.Value, ct);
            return CreatedAtAction(nameof(CreateWalkin), new { id = response.WalkinId }, response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Converts an anonymous walk-in to a full registered patient account.
    /// </summary>
    /// <param name="id">PK of the WalkIn record to convert.</param>
    /// <param name="request">Patient demographics for account creation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>New patient ID and conversion status.</returns>
    /// <response code="200">Conversion succeeded; new patient ID returned.</response>
    /// <response code="400">Validation failure or required field missing.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    /// <response code="404">No walk-in found with the given ID.</response>
    /// <response code="409">Walk-in has already been converted to a patient account.</response>
    [HttpPost("{id:guid}/convert")]
    [ProducesResponseType(typeof(ConvertWalkinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConvertWalkin(
        [FromRoute] Guid id,
        [FromBody] ConvertWalkinRequest request,
        CancellationToken ct)
    {
        var staffUserId = TryGetCurrentUserId();
        if (staffUserId is null)
            return Unauthorized(new { message = "Staff identity could not be resolved from token." });

        try
        {
            var response = await _walkinService.ConvertWalkinAsync(id, request, staffUserId.Value, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}

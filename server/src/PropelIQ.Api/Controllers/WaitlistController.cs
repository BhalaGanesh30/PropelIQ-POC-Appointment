using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Waitlist.Dto;
using PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Preferred-slot waitlist API (US_023).
///
/// POST   /api/v1/waitlist              — join the waitlist (AC-1).
/// GET    /api/v1/waitlist              — retrieve patient's active entries.
/// POST   /api/v1/waitlist/{id}/claim   — claim an offered slot (AC-3).
/// DELETE /api/v1/waitlist/{id}         — cancel a waitlist entry.
///
/// All endpoints require JWT authentication; patient ID is extracted from the
/// NameIdentifier claim — patient ownership is enforced at the repository level
/// (no cross-patient data exposure).
/// NFR-010: structured logging in WaitlistService captures all state transitions.
/// </summary>
[Authorize]
public sealed class WaitlistController : BaseApiController
{
    private readonly WaitlistService _waitlistService;

    public WaitlistController(WaitlistService waitlistService)
        => _waitlistService = waitlistService;

    private Guid GetPatientId() => TryGetCurrentUserId() ?? Guid.Empty;

    /// <summary>
    /// Join the waitlist with preferred slot parameters (AC-1).
    /// </summary>
    /// <response code="201">Entry created — includes ID, status, and FIFO position.</response>
    /// <response code="400">Validation failure — date range, duration, or type invalid.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WaitlistEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> JoinWaitlist(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken ct)
    {
        var result = await _waitlistService.JoinAsync(GetPatientId(), request, ct);
        return CreatedAtAction(nameof(GetEntries), null, result);
    }

    /// <summary>
    /// Get the current patient's Active and Offered waitlist entries.
    /// </summary>
    /// <response code="200">List of waitlist entries (may be empty).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<WaitlistEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEntries(CancellationToken ct)
    {
        var entries = await _waitlistService.GetEntriesAsync(GetPatientId(), ct);
        return Ok(entries);
    }

    /// <summary>
    /// Claim an offered slot to create a confirmed appointment (AC-3).
    /// Returns 409 Conflict when a concurrent claim already reserved the slot (edge case).
    /// </summary>
    /// <response code="200">Slot claimed — includes booking confirmation details.</response>
    /// <response code="400">Entry not offered, claim window expired, or not found.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="409">Slot taken by a concurrent claim — patient remains on waitlist.</response>
    [HttpPost("{id:guid}/claim")]
    [ProducesResponseType(typeof(ClaimWaitlistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClaimSlot(Guid id, CancellationToken ct)
    {
        var result = await _waitlistService.ClaimAsync(id, GetPatientId(), ct);

        if (result.IsSuccess)
            return Ok(result.Value);

        // Distinguish concurrent-claim conflict from validation errors.
        if (result.Error!.Contains("claimed by another patient", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Cancel (remove) a waitlist entry owned by the current patient.
    /// </summary>
    /// <response code="204">Entry cancelled successfully.</response>
    /// <response code="400">Entry is already claimed or cancelled.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="404">Entry not found or belongs to a different patient.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEntry(Guid id, CancellationToken ct)
    {
        var result = await _waitlistService.CancelEntryAsync(id, GetPatientId(), ct);

        if (result.IsSuccess)
            return NoContent();

        if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }
}

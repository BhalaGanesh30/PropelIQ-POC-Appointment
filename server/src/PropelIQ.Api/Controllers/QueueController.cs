using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Queue;
using PropelIQ.Modules.Scheduling.Application.Queue.Dto;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Real-time staff queue API (EP-004 US_031).
///
/// GET /api/v1/queue/today — returns all today's appointments enriched with
/// queue state, wait-time estimates, and the isOverdue flag.
///
/// AC-1: All today's appointments returned with status badges and wait-time estimates.
/// AC-2: Optional ?status= filter limits results to a single <see cref="QueueState"/>.
/// AC-3: <see cref="QueueEntryDto.IsOverdue"/> flag drives overdue row highlighting.
/// AC-4: Endpoint restricted to Staff and Admin roles.
/// Edge Case 2: Invalid ?status= value → HTTP 400 via [ApiController] model binding.
/// NFR-002: 15-second Redis cache ensures ≤500ms p95 after warm-up.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[Route("api/v1/queue")]
[ApiController]
[Produces("application/json")]
public sealed class QueueController : ControllerBase
{
    private readonly IQueueService _queueService;

    public QueueController(IQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Returns today's appointment queue enriched with queue state,
    /// wait-time estimates, and overdue indicators.
    /// </summary>
    /// <param name="status">
    /// Optional queue-state filter. Must be a valid <see cref="QueueState"/> value
    /// (Waiting, InProgress, Completed, NoShow).  Omit to return all states.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Queue response with sorted entries and a <c>generatedAt</c> timestamp.</returns>
    /// <response code="200">Queue data returned (may be empty when no patients are queued today).</response>
    /// <response code="400">Invalid <paramref name="status"/> value supplied.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    [HttpGet("today")]
    [ProducesResponseType(typeof(QueueResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTodayQueue(
        [FromQuery] QueueState? status,
        CancellationToken ct)
    {
        // Edge Case 2: [ApiController] returns HTTP 400 automatically when an
        // unrecognised string is supplied for the `status` enum parameter — no
        // manual guard required here.

        var result = await _queueService.GetTodayQueueAsync(status, ct);
        return Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Schedule.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Daily Schedule API (EP-004 US_036 FR-SO-006).
///
/// GET  /api/v1/schedule/daily?date={yyyy-MM-dd} — returns all appointments for the
///      requested date with patient names, types, and durations for the time-grid
///      calendar (AC-1). Redis-cached with 30-second TTL (AC-4 / NFR-002).
///
/// PUT  /api/v1/schedule/reschedule — updates appointment time after conflict
///      validation, creates an immutable audit record with the override reason and
///      staff identity, and invalidates the Redis cache for the affected date (AC-2).
///
/// AC-1: All appointments returned with patient names, types, and durations.
/// AC-2: Reschedule endpoint validates conflicts and creates audit record.
/// AC-4: Sub-1-second load via Redis caching (30s TTL).
/// Edge Case 1: HTTP 409 returned when target time slot is occupied.
/// Edge Case 2: Empty entries list returned for dates with no appointments.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[ApiController]
[Produces("application/json")]
public sealed class ScheduleController : BaseApiController
{
    private readonly IScheduleService _scheduleService;

    public ScheduleController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    /// <summary>
    /// Returns all appointments for the given calendar date, sorted by start time.
    /// Result is served from Redis on cache hit (30-second TTL, AC-4).
    /// </summary>
    /// <param name="date">
    /// Calendar date in <c>yyyy-MM-dd</c> format (e.g. <c>2026-05-06</c>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Schedule response with appointment entries sorted by start time (AC-1).
    /// Returns empty entries list for dates with no appointments (Edge Case 2).
    /// </returns>
    /// <response code="200">Schedule returned; entries list may be empty.</response>
    /// <response code="400">Invalid or missing <c>date</c> query parameter.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    [HttpGet("/api/v1/schedule/daily")]
    [ProducesResponseType(typeof(DailyScheduleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailySchedule(
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        var response = await _scheduleService.GetDailyScheduleAsync(date, ct);
        return Ok(response);
    }

    /// <summary>
    /// Reschedules an appointment to a new start time.
    /// Validates for time conflicts (Edge Case 1), creates an immutable audit record,
    /// and invalidates the Redis cache for the affected date(s) (AC-2).
    /// </summary>
    /// <param name="request">
    /// Reschedule payload: appointment ID, new start time (UTC), and mandatory
    /// override reason collected by the FE via <c>OverrideReasonDialogComponent</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Old start time, new start time, and the UUID of the created audit record.
    /// </returns>
    /// <response code="200">Reschedule successful.</response>
    /// <response code="400">Validation failure — missing fields or whitespace-only reason.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    /// <response code="404">Appointment not found.</response>
    /// <response code="409">
    /// Target time slot is occupied by another appointment (Edge Case 1).
    /// Response body contains the conflicting appointment's details.
    /// </response>
    [HttpPut("/api/v1/schedule/reschedule")]
    [ProducesResponseType(typeof(RescheduleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DailyScheduleEntryDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(
        [FromBody] RescheduleRequestDto request,
        CancellationToken ct)
    {
        var staffUserId = TryGetCurrentUserId();
        if (staffUserId is null)
            return Unauthorized();

        try
        {
            var response = await _scheduleService.RescheduleAsync(
                request, staffUserId.Value, ct);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ScheduleConflictException ex)
        {
            // Edge Case 1: return 409 with conflicting appointment details.
            return Conflict(ex.ConflictingEntry);
        }
    }
}

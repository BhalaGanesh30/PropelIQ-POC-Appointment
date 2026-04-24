using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Scheduling;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Exposes appointment slot search for both Patient and Staff personas.
/// GET /api/v1/appointments/slots — returns available slots grouped by date.
/// Requires JWT bearer authentication (AC-1 implied by [Authorize]).
/// </summary>
[Authorize]
public sealed class AppointmentsController : BaseApiController
{
    private readonly ISlotSearchService _slotSearchService;

    public AppointmentsController(ISlotSearchService slotSearchService)
        => _slotSearchService = slotSearchService;

    /// <summary>
    /// Search available appointment slots by date range, duration, and type.
    /// </summary>
    /// <param name="query">Search parameters: dateFrom, dateTo, duration (15/30/60), type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Available slots grouped by date, or empty result with hasResults=false (AC-3).</returns>
    /// <response code="200">Slots returned (may be empty when no availability — AC-3).</response>
    /// <response code="400">Validation failure — e.g. date range exceeds 30 days (AC-4).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpGet("slots")]
    [ProducesResponseType(typeof(SlotSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchSlots(
        [FromQuery] SlotSearchQuery query,
        CancellationToken ct)
    {
        // FluentValidation auto-validates via [ApiController] + AddFluentValidationAutoValidation.
        // A 400 with validation errors is returned automatically for invalid queries (AC-4).
        var result = await _slotSearchService.SearchAsync(query, ct);
        return Ok(result);
    }
}

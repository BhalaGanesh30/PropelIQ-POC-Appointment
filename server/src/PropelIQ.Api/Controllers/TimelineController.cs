using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Clinical timeline API — aggregates events from clinical facts and documents
/// for the patient 360° view (US_048, FR-CA-005, SCR-015).
///
/// Route: GET /api/v1/patients/{id}/timeline
///
/// Authorization: Clinician (read) or Staff (read-only access per SCR-015 access matrix).
///
/// Behaviours mandated by US_048:
///   AC-1: Events returned in reverse-chronological order covering medications,
///         diagnoses, allergies, and documents.
///   AC-2: category filter narrows results; response within 500 ms p95 (NFR-002).
///   AC-3: dateFrom/dateTo filters applied server-side; inclusive range.
///   AC-4: FE reuses this endpoint with active filters for print rendering —
///         no separate print endpoint needed.
///   Edge Case 1: Empty timeline → HTTP 200 with { events: [], totalCount: 0 }.
///   Edge Case 2: Large timelines → server-side filtering; totalCount informs FE grouping.
/// </summary>
[Authorize(Roles = "Clinician,Staff")]
[Route("api/v1/patients")]
[ApiController]
[Produces("application/json")]
public sealed class TimelineController : BaseApiController
{
    private readonly ITimelineService _timelineService;

    public TimelineController(ITimelineService timelineService)
    {
        _timelineService = timelineService;
    }

    /// <summary>
    /// Returns the clinical timeline for the specified patient.
    /// Always HTTP 200 — even for patients with no events (Edge Case 1).
    /// </summary>
    /// <param name="id">Patient GUID.</param>
    /// <param name="category">
    /// Optional category filter: "Medications", "Allergies", "Diagnoses", "Findings",
    /// "Documents", or null / "All" for all sources (AC-2).
    /// </param>
    /// <param name="dateFrom">
    /// Optional ISO 8601 start date (inclusive). Must be &lt;= <paramref name="dateTo"/> when
    /// both are provided — returns HTTP 400 otherwise (AC-3).
    /// </param>
    /// <param name="dateTo">Optional ISO 8601 end date (inclusive, AC-3).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>HTTP 200 with <see cref="TimelineResponseDto"/>.</returns>
    /// <response code="200">Timeline events (may be an empty list).</response>
    /// <response code="400">Invalid patient ID or contradictory date range.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller lacks Clinician or Staff role.</response>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType(typeof(TimelineResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTimelineAsync(
        [FromRoute]  Guid            id,
        [FromQuery]  string?         category = null,
        [FromQuery]  DateTimeOffset? dateFrom = null,
        [FromQuery]  DateTimeOffset? dateTo   = null,
        CancellationToken ct = default)
    {
        // Validate date range order — dateFrom must not be after dateTo (AC-3).
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
        {
            return BadRequest(new { error = "dateFrom must be less than or equal to dateTo." });
        }

        var query = new TimelineQuery
        {
            Category = category,
            DateFrom = dateFrom,
            DateTo   = dateTo,
        };

        var response = await _timelineService.GetTimelineAsync(id, query, ct);

        // Always HTTP 200 — empty events list is a valid response (Edge Case 1).
        return Ok(response);
    }
}

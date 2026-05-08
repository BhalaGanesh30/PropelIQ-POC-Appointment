using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Patient search API for walk-in disambiguation (EP-004 US_033 Edge Case 1).
///
/// GET /api/v1/patients/search?q= — returns up to 10 matching patients with
/// demographics (name, DOB, phone) so staff can identify the correct record
/// before creating a walk-in.
///
/// FR-SO-003: Restricted to Staff and Admin roles.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[Route("api/v1/patients")]
[ApiController]
[Produces("application/json")]
public sealed class PatientSearchController : ControllerBase
{
    private readonly IPatientSearchService _patientSearchService;

    public PatientSearchController(IPatientSearchService patientSearchService)
    {
        _patientSearchService = patientSearchService;
    }

    /// <summary>
    /// Searches existing patients by name or phone number.
    /// </summary>
    /// <param name="q">
    /// Search query — must be at least 2 characters. Matched case-insensitively
    /// against patient full name and preferred phone.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to 10 matching patients ordered by last name, first name.</returns>
    /// <response code="200">Matching patients returned (may be empty).</response>
    /// <response code="400">Query string is missing or shorter than 2 characters.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string? q,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { message = "Query parameter 'q' must be at least 2 characters." });

        var results = await _patientSearchService.SearchAsync(q.Trim(), ct);
        return Ok(results);
    }
}

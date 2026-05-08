using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// 360° patient profile API — aggregates clinical facts by category for a given patient.
///
/// Route: GET /api/v1/patients/{id}/profile
///
/// Authorization: Clinician (read/write access to clinical data) or Staff (read-only).
/// Anonymous or insufficient-role callers receive HTTP 401/403.
///
/// Behaviours mandated by SCR-014:
///   AC-1: Returns all fact categories (medications, allergies, diagnoses, findings)
///         in under 500ms p95 (Redis cached, NFR-002).
///   AC-4: Empty profiles return HTTP 200 with empty fact collections (never 404).
///   Edge Case 1: Partial failure returns HTTP 200 with available data plus
///                X-Partial-Content: true header and populated partialSources array.
///   Edge Case 2: Pagination via limit/offset supports large profiles (100+ facts).
/// </summary>
[Authorize(Roles = "Clinician,Staff")]
[Route("api/v1/patients")]
[ApiController]
[Produces("application/json")]
public sealed class PatientProfileController : BaseApiController
{
    private readonly IPatientProfileAggregationService _aggregationService;

    public PatientProfileController(IPatientProfileAggregationService aggregationService)
    {
        _aggregationService = aggregationService;
    }

    /// <summary>
    /// Returns the 360° patient profile: medications, allergies, diagnoses, findings,
    /// and a chronological timeline, all with per-fact source traceability.
    /// </summary>
    /// <param name="id">Patient GUID.</param>
    /// <param name="limit">
    /// Maximum facts to return per category. Must be 1–100. Defaults to 50. (Edge Case 2)
    /// </param>
    /// <param name="offset">Zero-based offset for pagination. Defaults to 0.</param>
    /// <param name="tab">Active tab hint ("summary", "timeline", or a fact type).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// HTTP 200 with <see cref="PatientProfileDto"/> — always, including empty profiles (AC-4).
    /// </returns>
    /// <response code="200">Profile returned (may be empty or partial).</response>
    /// <response code="400">Invalid patient ID or limit out of range.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller lacks Clinician or Staff role.</response>
    [HttpGet("{id:guid}/profile")]
    [ProducesResponseType(typeof(PatientProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProfileAsync(
        [FromRoute] Guid id,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string tab = "summary",
        CancellationToken ct = default)
    {
        // Validate limit (Edge Case 2 — prevents runaway queries).
        if (limit is < 1 or > 100)
        {
            return BadRequest(new { error = "Limit must be between 1 and 100." });
        }

        if (offset < 0)
        {
            return BadRequest(new { error = "Offset must be 0 or greater." });
        }

        var query = new ProfileQuery
        {
            Limit  = limit,
            Offset = offset,
            Tab    = tab,
        };

        var profile = await _aggregationService.AggregateProfileAsync(id, query, ct);

        // Signal partial data to the FE so it can display the warning banner (Edge Case 1).
        if (profile.Partial)
        {
            Response.Headers["X-Partial-Content"] = "true";
        }

        return Ok(profile);
    }
}

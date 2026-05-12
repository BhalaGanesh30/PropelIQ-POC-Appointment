using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// CPT and E/M coding suggestion API (US_050).
///
/// GET /api/v1/patients/{id}/coding-suggestions/cpt?appointmentId={appointmentId}
///
/// Returns AI-generated CPT procedure code and E/M level suggestions for the given patient
/// and appointment.  The endpoint always returns HTTP 200 — domain edge cases are
/// communicated via response body flags:
/// - <c>noSuggestionForAppointmentType: true</c> — appointment type is not CPT-mappable (Edge Case 1).
/// - <c>staleDatabaseWarning: true</c>           — CPT catalog is older than 90 days (Edge Case 2).
/// - <c>lowConfidence: true</c>                  — AI confidence below threshold (AC-4, AIR-005).
///
/// Access control: Clinician role required (AC-1).
/// </summary>
[Authorize(Roles = "Clinician")]
[Route("api/v1/patients")]
public sealed class CptSuggestionController : BaseApiController
{
    private readonly ICptSuggestionOrchestrator _orchestrator;

    public CptSuggestionController(ICptSuggestionOrchestrator orchestrator)
        => _orchestrator = orchestrator;

    /// <summary>
    /// Returns ranked CPT procedure code and E/M level suggestions for the given patient
    /// and appointment (US_050 AC-1 through AC-4).
    ///
    /// - Suggestions are validated against the live CPT reference catalog (no deprecated codes).
    /// - <c>lowConfidence: true</c> when top CPT confidence is below the configured threshold.
    /// - <c>staleDatabaseWarning: true</c> when the CPT catalog has not been updated in 90+ days.
    /// - <c>noSuggestionForAppointmentType: true</c> when the appointment type has no CPT mapping.
    ///
    /// Redis cache TTL: 90 seconds per patient+appointment combination.
    /// </summary>
    /// <param name="id">Patient GUID from the route.</param>
    /// <param name="appointmentId">Appointment GUID from the query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">CPT/E/M suggestions (may include warning flags).</response>
    /// <response code="400">Missing or invalid appointmentId query parameter.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    [HttpGet("{id:guid}/coding-suggestions/cpt")]
    [ProducesResponseType(typeof(CptSuggestionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCptSuggestions(
        [FromRoute] Guid id,
        [FromQuery] Guid appointmentId,
        CancellationToken ct)
    {
        if (appointmentId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Invalid Request",
                Detail = "appointmentId query parameter is required and must be a valid GUID.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity("cpt_suggestion.controller");
        activity?.SetTag("patient.id", id);
        activity?.SetTag("appointment.id", appointmentId);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _orchestrator.GenerateCptSuggestionsAsync(id, appointmentId, ct);
        sw.Stop();

        activity?.SetTag("coding.suggestion.duration_ms", sw.ElapsedMilliseconds);

        return Ok(response);
    }
}

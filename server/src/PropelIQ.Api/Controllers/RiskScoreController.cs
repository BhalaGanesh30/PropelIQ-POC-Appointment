using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.AI;
using PropelIQ.Modules.Scheduling.Application.AI.Models;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Exposes no-show risk scores for the staff queue dashboard (SCR-025).
/// GET /api/v1/appointments/risk-scores returns cached or freshly-scored risk data
/// for all non-cancelled appointments within the requested date range.
///
/// AC-4: Scores are returned from cache when fresh (24h TTL).
///       Stale or missing scores are calculated inline — still within 2.5s p95
///       because the AI task_001 layer handles caching.
/// Authorization: Staff role only — risk data is clinically sensitive.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[Route("api/v1/appointments")]
[ApiController]
[Produces("application/json")]
public sealed class RiskScoreController : ControllerBase
{
    private readonly INoShowRiskScoringService _scoringService;
    private readonly IBookingRepository _bookingRepository;

    public RiskScoreController(
        INoShowRiskScoringService scoringService,
        IBookingRepository bookingRepository)
    {
        _scoringService = scoringService;
        _bookingRepository = bookingRepository;
    }

    /// <summary>
    /// Returns risk scores for upcoming appointments in the specified date range.
    /// Stale scores (&gt;24h) are recalculated inline before being returned.
    /// </summary>
    /// <param name="from">Start of the date range (inclusive, UTC).</param>
    /// <param name="to">End of the date range (inclusive, UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of appointments with their no-show risk assessment.</returns>
    /// <response code="200">Risk scores returned successfully.</response>
    /// <response code="400">Missing or invalid date range parameters.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    [HttpGet("risk-scores")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentRiskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRiskScores(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        if (from >= to)
            return BadRequest(new { error = "'from' must be earlier than 'to'." });

        var appointments = await _bookingRepository
            .GetUpcomingForRiskDashboardAsync(from, to, ct);

        var results = new List<AppointmentRiskDto>(appointments.Count);

        foreach (var appt in appointments)
        {
            // ScoreAsync returns a cached result when the score is fresh (AC-4).
            // It falls back to Unknown without throwing when the gateway is down.
            var score = await _scoringService.ScoreAsync(appt.Id, ct);

            results.Add(new AppointmentRiskDto(
                AppointmentId:   appt.Id,
                PatientName:     appt.PatientName,
                AppointmentDate: appt.ScheduledAt,
                AppointmentType: appt.AppointmentType,
                Status:          appt.Status,
                RiskLevel:       score.RiskLevel,
                Confidence:      score.Confidence,
                Features:        score.Features
                    .Select(f => new RiskFeatureDto(f.Name, f.Contribution))
                    .ToList()));
        }

        return Ok(results);
    }
}

/// <summary>Response DTO for a single appointment's risk assessment.</summary>
public sealed record AppointmentRiskDto(
    Guid AppointmentId,
    string PatientName,
    DateTimeOffset AppointmentDate,
    string AppointmentType,
    string Status,
    string RiskLevel,
    double Confidence,
    IReadOnlyList<RiskFeatureDto> Features);

/// <summary>A single explainable feature contribution for a risk score.</summary>
public sealed record RiskFeatureDto(
    string Name,
    string Contribution);

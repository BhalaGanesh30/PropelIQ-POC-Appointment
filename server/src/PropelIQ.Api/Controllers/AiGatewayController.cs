using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// AI gateway circuit breaker status endpoint (US_053, Edge Case 2).
///
/// Endpoint:
///   <c>GET /api/v1/ai-gateway/status</c>
///     — Returns the current circuit state, fallback-active flag, and last trip timestamp.
///       Consumed by the Angular <see cref="AiGatewayStatusFacade"/> polling loop (30s interval)
///       to drive the AI fallback banner (AC-2, AC-3).
///
/// Authorization: Clinician or Staff roles (circuit state is not PHI but is role-relevant).
/// </summary>
[Authorize(Roles = "Clinician,Staff")]
public sealed class AiGatewayController : BaseApiController
{
    private readonly IAiGatewayStateService _stateService;

    public AiGatewayController(IAiGatewayStateService stateService)
        => _stateService = stateService;

    /// <summary>
    /// Returns the current AI gateway circuit breaker state.
    ///
    /// Response model:
    /// <code>
    /// {
    ///   "circuitState": "closed" | "open" | "half-open",
    ///   "fallbackActive": true | false,
    ///   "lastTripAt": "2026-05-11T10:30:00Z" | null
    /// }
    /// </code>
    ///
    /// The <c>fallbackActive</c> flag is <c>true</c> when <c>circuitState</c> is <c>"open"</c>
    /// or <c>"half-open"</c> — the FE uses this to show or hide the amber fallback banner
    /// without parsing the state string (AC-2, AC-3).
    ///
    /// The <c>lastTripAt</c> timestamp helps operations teams correlate circuit trips with
    /// AI provider incidents (AIR-011 audit trail).
    /// </summary>
    /// <response code="200">Current circuit breaker status.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have Clinician or Staff role.</response>
    [HttpGet("ai-gateway/status")]
    [ProducesResponseType(typeof(AiGatewayStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _stateService.GetStatusAsync(ct);
        return Ok(status);
    }
}

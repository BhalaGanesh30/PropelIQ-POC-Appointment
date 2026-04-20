using Microsoft.AspNetCore.Mvc;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Health check endpoint satisfying US_002 AC-2.
/// GET /api/v1/health returns HTTP 200 with a JSON health status payload.
/// </summary>
public sealed class HealthController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new HealthResponse("healthy", DateTimeOffset.UtcNow));
    }
}

/// <param name="Status">Service health status string.</param>
/// <param name="Timestamp">UTC timestamp of the health check response.</param>
public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);

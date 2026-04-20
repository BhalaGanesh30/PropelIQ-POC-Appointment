using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Diagnostics endpoints used to verify the authentication and exception
/// middleware behaviours during integration testing (US_002 AC-4).
/// </summary>
public sealed class DiagnosticsController : BaseApiController
{
    /// <summary>
    /// Protected endpoint — returns HTTP 401 with Problem Details JSON for
    /// unauthenticated requests (US_002 AC-4).
    /// GET /api/v1/diagnostics/protected
    /// </summary>
    [HttpGet("protected")]
    [Authorize]
    public IActionResult Protected()
    {
        return Ok(new { message = "Authenticated access confirmed." });
    }

    /// <summary>
    /// Unprotected endpoint for smoke-testing the pipeline.
    /// GET /api/v1/diagnostics/ping
    /// </summary>
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        return Ok(new { message = "pong", timestamp = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Triggers a deliberate unhandled exception to verify global exception
    /// middleware returns HTTP 500 Problem Details (US_002 Edge Case).
    /// GET /api/v1/diagnostics/error (development only)
    /// </summary>
    [HttpGet("error")]
    [AllowAnonymous]
    public IActionResult TriggerError()
    {
        throw new InvalidOperationException(
            "Deliberate test exception — verifies RFC 9457 Problem Details response.");
    }
}

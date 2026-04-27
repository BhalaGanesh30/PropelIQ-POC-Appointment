using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Base controller applying versioned route prefix to all derived controllers.
/// Satisfies TR-002: all REST endpoints exposed under /api/v1/[controller].
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Returns the authenticated user's ID from the JWT sub claim.
    /// Returns null when the claim is absent — callers should return 401.
    /// Handles both JsonWebTokenHandler (raw claim names) and JwtSecurityTokenHandler
    /// (mapped claim names) so the controller is resilient to JWT handler changes.
    /// </summary>
    protected Guid? TryGetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        return value is not null && Guid.TryParse(value, out var id) ? id : null;
    }
}

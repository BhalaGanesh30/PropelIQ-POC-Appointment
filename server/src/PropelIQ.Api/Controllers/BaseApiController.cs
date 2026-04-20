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
}

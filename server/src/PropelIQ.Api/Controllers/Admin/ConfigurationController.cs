using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.SharedServices.Application.Configuration;
using System.Security.Claims;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin-only configuration management REST API (US_059, AC-1–AC-4, edge cases 1–2).
///
/// <para>Endpoints:</para>
/// <list type="bullet">
///   <item><c>GET  /api/v1/admin/config/{category}</c>                — current configuration snapshot with ETag.</item>
///   <item><c>PUT  /api/v1/admin/config/{category}</c>                — update with OCC via If-Match header.</item>
///   <item><c>GET  /api/v1/admin/config/{category}/history</c>        — full version history (AC-3).</item>
///   <item><c>POST /api/v1/admin/config/{category}/restore/{versionId}</c> — rollback to historical version (AC-4).</item>
/// </list>
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/config")]
[ApiController]
[Produces("application/json")]
public sealed class ConfigurationController : BaseApiController
{
    private readonly IConfigurationService _service;

    public ConfigurationController(IConfigurationService service)
        => _service = service;

    /// <summary>
    /// Returns the current active configuration snapshot for the given category.
    /// Sets an <c>ETag</c> response header with the current version number for use
    /// in subsequent <c>If-Match</c> headers (OCC, edge case 1).
    /// </summary>
    [HttpGet("{category}")]
    [ProducesResponseType(typeof(ConfigurationSnapshot), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetCurrent(
        ConfigurationCategory category,
        CancellationToken ct)
    {
        var snapshot = await _service.GetCurrentAsync(category, ct);
        Response.Headers["ETag"] = $"\"{snapshot.VersionNumber}\"";
        return Ok(snapshot);
    }

    /// <summary>
    /// Updates the configuration for the given category.
    /// Requires <c>If-Match</c> header containing the version number returned by the last GET.
    /// Returns 409 Conflict when the version has changed since the admin loaded the form (edge case 1).
    /// Returns 422 when submitted values fail validation constraints (AC-2).
    /// </summary>
    [HttpPut("{category}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Update(
        ConfigurationCategory category,
        [FromBody] ConfigurationUpdateRequest request,
        CancellationToken ct)
    {
        if (TryGetCurrentUserId() is not Guid adminId)
            return Unauthorized();

        if (!Request.Headers.TryGetValue("If-Match", out var etagHeader))
            return BadRequest("If-Match header is required for configuration updates.");

        if (!int.TryParse(etagHeader.ToString().Trim('"'), out var expectedVersion))
            return BadRequest("If-Match header must be a quoted integer version number.");

        request = request with
        {
            ExpectedVersion = expectedVersion,
            AdminId         = adminId,
            AdminName       = User.FindFirstValue(ClaimTypes.Name)
                           ?? User.FindFirstValue("email")
                           ?? "Unknown"
        };

        var result = await _service.UpdateAsync(category, request, ct);

        if (result.ConflictDetected)
            return Conflict(result.CurrentValue);

        if (result.ValidationErrors?.Count > 0)
            return UnprocessableEntity(result.ValidationErrors);

        return Ok(new { result.VersionId, result.VersionNumber });
    }

    /// <summary>
    /// Returns the full version history for the given category, newest first (AC-3).
    /// Each entry includes the before/after diff, timestamp, and admin identity.
    /// </summary>
    [HttpGet("{category}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigurationVersionDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetHistory(
        ConfigurationCategory category,
        CancellationToken ct)
    {
        var history = await _service.GetHistoryAsync(category, ct);
        return Ok(history);
    }

    /// <summary>
    /// Restores a previous configuration version as a new current version without overwriting
    /// history (AC-4). Validates the restored snapshot against current business rules first.
    /// Returns 422 if the restored values no longer satisfy current constraints.
    /// </summary>
    [HttpPost("{category}/restore/{versionId:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Restore(
        ConfigurationCategory category,
        Guid versionId,
        CancellationToken ct)
    {
        if (TryGetCurrentUserId() is not Guid adminId)
            return Unauthorized();

        var result = await _service.RestoreVersionAsync(category, versionId, adminId, ct);

        if (result.ValidationErrors?.Count > 0)
            return UnprocessableEntity(result.ValidationErrors);

        if (!result.Success)
            return NotFound(result.ValidationErrors?.FirstOrDefault() ?? "Version not found.");

        return Ok(new { result.VersionId, result.VersionNumber });
    }
}

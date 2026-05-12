using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.SharedServices.Application.Templates;
using PropelIQ.Modules.SharedServices.Application.Templates.Validators;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin-only REST API for versioned HTML and SMS notification template management
/// (US_062, AC-1–AC-4, edge cases 1–2).
///
/// <list type="bullet">
///   <item><c>GET  /api/v1/admin/templates</c>                              — paginated template list.</item>
///   <item><c>GET  /api/v1/admin/templates/{id}</c>                         — template detail with current version.</item>
///   <item><c>GET  /api/v1/admin/templates/{id}/versions</c>                — full version history.</item>
///   <item><c>POST /api/v1/admin/templates/{id}</c>                         — save (creates new immutable version, AC-1).</item>
///   <item><c>POST /api/v1/admin/templates/{id}/preview</c>                 — preview with sample merge values (AC-2).</item>
///   <item><c>POST /api/v1/admin/templates/{id}/restore/{versionId}</c>     — restore old version as new active (AC-3).</item>
///   <item><c>POST /api/v1/admin/templates/{id}/validate</c>                — validate merge-field placeholders (AC-4).</item>
/// </list>
///
/// All endpoints require the <c>Admin</c> role.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/templates")]
[ApiController]
[Produces("application/json")]
public sealed class TemplatesController : BaseApiController
{
    private readonly ITemplateManagementService         _svc;
    private readonly IValidator<SaveTemplateRequest>    _saveValidator;

    public TemplatesController(
        ITemplateManagementService          svc,
        IValidator<SaveTemplateRequest>     saveValidator)
    {
        _svc           = svc;
        _saveValidator = saveValidator;
    }

    // ── List ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated list of notification templates, optionally filtered by
    /// <paramref name="typeFilter"/> ("HTML" or "SMS").
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TemplatePagedResult<TemplateListItemDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> List(
        [FromQuery] string? typeFilter = null,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 25,
        CancellationToken ct = default)
    {
        var result = await _svc.ListAsync(typeFilter, page, pageSize, ct);
        return Ok(result);
    }

    // ── GetById ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns full detail for a single template including its currently active version.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TemplateDetailDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await _svc.GetByIdAsync(id, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── GetVersions ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the paginated version history for a template (newest first).
    /// </summary>
    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(List<TemplateVersionDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVersions(
        Guid id,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _svc.GetVersionsAsync(id, page, pageSize, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Save (AC-1, AC-4) ────────────────────────────────────────────────────

    /// <summary>
    /// Validates merge-field placeholders and creates a new immutable version (AC-1).
    /// Returns <c>422 Unprocessable Entity</c> when the content contains unknown
    /// placeholders (AC-4).
    /// </summary>
    [HttpPost("{id:guid}")]
    [ProducesResponseType(typeof(TemplateVersionDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Save(
        Guid                  id,
        [FromBody] SaveTemplateRequest request,
        CancellationToken ct = default)
    {
        var validation = await _saveValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return UnprocessableEntity(validation.Errors.Select(e => e.ErrorMessage));

        var adminId = TryGetCurrentUserId();
        if (adminId is null)
            return Unauthorized();

        // Read display name from the Name claim; fall back to the UUID string so the
        // version history always has a non-null creator label (AC-1).
        var adminName = User.Identity?.Name ?? adminId.Value.ToString();

        try
        {
            var result = await _svc.SaveAsync(id, request, adminId.Value, adminName, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    // ── Preview (AC-2) ───────────────────────────────────────────────────────

    /// <summary>
    /// Renders a draft template body with sample merge-field values substituted (AC-2).
    /// No database write occurs.
    /// </summary>
    [HttpPost("{id:guid}/preview")]
    [ProducesResponseType(typeof(PreviewResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromBody] PreviewRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _svc.PreviewAsync(id, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Restore (AC-3) ───────────────────────────────────────────────────────

    /// <summary>
    /// Copies the content of an existing version into a new active version (AC-3).
    /// Queued notifications referencing the original version ID are unaffected.
    /// </summary>
    [HttpPost("{id:guid}/restore/{versionId:guid}")]
    [ProducesResponseType(typeof(TemplateVersionDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Restore(
        Guid id,
        Guid versionId,
        CancellationToken ct = default)
    {
        var adminId = TryGetCurrentUserId();
        if (adminId is null)
            return Unauthorized();

        var adminName = User.Identity?.Name ?? adminId.Value.ToString();

        try
        {
            var result = await _svc.RestoreVersionAsync(id, versionId, adminId.Value, adminName, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Validate (AC-4, edge case 2) ─────────────────────────────────────────

    /// <summary>
    /// Validates the merge-field placeholders in the provided content without
    /// persisting anything (AC-4, edge case 2).
    /// </summary>
    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType(typeof(TemplateValidationResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Validate(
        Guid id,
        [FromBody] string content,
        CancellationToken ct = default)
    {
        var result = await _svc.ValidateAsync(content, ct);
        return Ok(result);
    }
}

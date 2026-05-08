using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Authorized clinical fact editing and verification API (US_047 / FR-CA-004).
///
/// Routes:
///   PATCH /api/v1/clinical-facts/{id}         — Edit name/value with optimistic concurrency.
///   POST  /api/v1/clinical-facts/{id}/verify  — Mark fact as verified.
///   GET   /api/v1/clinical-facts/{id}/history — Return chronological audit history.
///
/// Access matrix:
///   Clinician — PATCH, POST /verify, GET /history.
///   Staff     — GET /history only.
///   Patient   — HTTP 403 on all write endpoints (AC-4).
///   Anonymous — HTTP 401 on all endpoints.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class ClinicalFactsController : BaseApiController
{
    private readonly IFactEditingService _editingService;

    public ClinicalFactsController(IFactEditingService editingService)
    {
        _editingService = editingService;
    }

    /// <summary>
    /// Edits the name and/or value of a clinical fact (AC-1, Edge Case 1 &amp; 2).
    ///
    /// Requires the <c>If-Match</c> header containing the current <c>row_version</c> ETag.
    /// Returns HTTP 428 when the header is absent, HTTP 409 when a concurrent edit has
    /// already changed the fact, and HTTP 200 with the updated fact on success.
    /// </summary>
    /// <param name="id">Fact GUID.</param>
    /// <param name="request">Patch request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Fact updated. Response body contains the new fact with fresh ETag.</response>
    /// <response code="400">Request body fails validation (both Name and Value are null).</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller lacks Clinician role.</response>
    /// <response code="404">Fact not found.</response>
    /// <response code="409">Optimistic concurrency conflict — another writer updated the fact first.</response>
    /// <response code="428">Missing <c>If-Match</c> header (Precondition Required).</response>
    [HttpPatch("api/v1/clinical-facts/{id:guid}")]
    [Authorize(Roles = "Clinician")]
    [ProducesResponseType(typeof(ClinicalFactResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ClinicalFactResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> PatchFactAsync(
        [FromRoute] Guid id,
        [FromBody]  PatchFactRequest request,
        CancellationToken ct = default)
    {
        var editorId = TryGetCurrentUserId();
        if (editorId is null)
            return Unauthorized();

        // Require If-Match header — absence is HTTP 428 Precondition Required (spec §6).
        var ifMatch = Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return StatusCode(
                StatusCodes.Status428PreconditionRequired,
                new { error = "If-Match header is required for optimistic concurrency control." });
        }

        // ETag is stored as plain integer string representation of row_version.
        if (!int.TryParse(ifMatch.Trim('"'), out var expectedRowVersion))
        {
            return BadRequest(new { error = "If-Match header value must be a valid integer ETag." });
        }

        var result = await _editingService.EditAsync(id, request, expectedRowVersion, editorId.Value, ct);

        return result switch
        {
            EditResult.NotFound                   => NotFound(new { error = $"Clinical fact {id} not found." }),
            EditResult.Conflict { CurrentFact: var current } => Conflict(current),
            EditResult.Success  { Dto: var dto }  => OkWithETag(dto),
            _                                     => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Marks a clinical fact as verified without changing its content (AC-2).
    ///
    /// Sets <c>verified = true</c>, records <c>verified_by</c> and <c>verified_at</c>,
    /// and writes an audit record. No <c>If-Match</c> header is required.
    /// </summary>
    /// <param name="id">Fact GUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Fact verified. Response body contains the updated fact with fresh ETag.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller lacks Clinician role.</response>
    /// <response code="404">Fact not found.</response>
    [HttpPost("api/v1/clinical-facts/{id:guid}/verify")]
    [Authorize(Roles = "Clinician")]
    [ProducesResponseType(typeof(ClinicalFactResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyFactAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var verifierId = TryGetCurrentUserId();
        if (verifierId is null)
            return Unauthorized();

        var dto = await _editingService.VerifyAsync(id, verifierId.Value, ct);
        if (dto is null)
            return NotFound(new { error = $"Clinical fact {id} not found." });

        return OkWithETag(dto);
    }

    /// <summary>
    /// Returns the chronological edit and verify history for a clinical fact (AC-3).
    ///
    /// Returns an empty list when no audit records exist — never returns 404 for
    /// an existing fact with no history.
    /// </summary>
    /// <param name="id">Fact GUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Audit history returned (may be empty).</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller lacks Clinician or Staff role.</response>
    [HttpGet("api/v1/clinical-facts/{id:guid}/history")]
    [Authorize(Roles = "Clinician,Staff")]
    [ProducesResponseType(typeof(IReadOnlyList<FactAuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var history = await _editingService.GetHistoryAsync(id, ct);
        return Ok(history);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns HTTP 200 and sets the <c>ETag</c> response header from the DTO.</summary>
    private OkObjectResult OkWithETag(ClinicalFactResponseDto dto)
    {
        Response.Headers.ETag = $"\"{dto.ETag}\"";
        return Ok(dto);
    }
}

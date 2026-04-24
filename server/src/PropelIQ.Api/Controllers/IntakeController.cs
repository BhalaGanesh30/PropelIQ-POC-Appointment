using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.AI.Dto;
using PropelIQ.Modules.Scheduling.Application.Intake.Dto;
using PropelIQ.Modules.Scheduling.Infrastructure.AI;
using PropelIQ.Modules.Scheduling.Infrastructure.Intake;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Intake draft API — autosave, retrieve, submit, and AI-assisted prefill.
///
/// PUT  /api/v1/intake/draft       — Autosave partial form data on blur (AC-2)
/// GET  /api/v1/intake/draft       — Retrieve saved draft for resume (AC-3)
/// POST /api/v1/intake/submit      — Validate and attach intake to booking (AC-4)
/// POST /api/v1/intake/ai-assist   — AI-prefill structured fields from free text (AC-1)
///
/// All endpoints are scoped to the authenticated patient via JWT sub claim.
/// NFR-010: operations are auditable via AppDbContext SaveChanges interceptors.
/// </summary>
[Authorize]
public sealed class IntakeController : BaseApiController
{
    private readonly IntakeDraftService _intakeService;
    private readonly IntakeAssistService _assistService;

    public IntakeController(
        IntakeDraftService intakeService,
        IntakeAssistService assistService)
    {
        _intakeService = intakeService;
        _assistService = assistService;
    }

    private Guid GetPatientId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Autosave partial form data on each blur event (AC-2).
    /// Creates a new draft or updates the existing draft for this patient+slot.
    /// </summary>
    /// <response code="200">Draft saved — includes draftId and savedAt timestamp.</response>
    /// <response code="400">FormData is null or malformed.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpPut("draft")]
    [ProducesResponseType(typeof(SaveDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveDraft(
        [FromBody] SaveDraftRequest request,
        CancellationToken ct)
    {
        var result = await _intakeService.SaveDraftAsync(GetPatientId(), request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve the patient's saved draft to resume the intake form (AC-3).
    /// When slotId is provided, returns the draft for that slot; otherwise returns the most recent draft.
    /// </summary>
    /// <param name="slotId">Optional: filter to a specific appointment slot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Draft found — includes form data and AI-populated field list.</response>
    /// <response code="204">No draft exists for this patient / slot combination.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpGet("draft")]
    [ProducesResponseType(typeof(IntakeDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDraft(
        [FromQuery] Guid? slotId,
        CancellationToken ct)
    {
        var draft = await _intakeService.GetDraftAsync(GetPatientId(), slotId, ct);
        return draft is null ? NoContent() : Ok(draft);
    }

    /// <summary>
    /// Finalize and submit the intake form, attaching it to the appointment booking (AC-4).
    /// Transitions the draft status to Submitted and creates an IntakeRecord.
    /// </summary>
    /// <response code="200">Intake submitted — includes intakeRecordId and submittedAt.</response>
    /// <response code="400">Validation failure — missing draftId or appointmentId.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Draft belongs to a different patient.</response>
    /// <response code="404">Draft not found or already submitted.</response>
    [HttpPost("submit")]
    [ProducesResponseType(typeof(SubmitIntakeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitIntake(
        [FromBody] SubmitIntakeRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _intakeService.SubmitIntakeAsync(
                GetPatientId(), request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// AI-assisted intake prefill — extracts structured fields from a free-text
    /// symptom description and returns field-level suggestions within 2.5 s (AC-1, AIR-006).
    ///
    /// When the AI gateway is unavailable or returns low-confidence output the
    /// response contains <c>aiAssisted: false</c> and a <c>fallbackReason</c>
    /// so the frontend can display "AI assist unavailable, please fill in manually." (AIR-005).
    /// Patient identifiers are never forwarded to the AI gateway (AIR-009).
    /// </summary>
    /// <response code="200">
    /// Suggestions returned — <c>aiAssisted</c> indicates whether the AI succeeded.
    /// </response>
    /// <response code="400">Request body is missing or <c>freeTextDescription</c> is absent.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpPost("ai-assist")]
    [ProducesResponseType(typeof(IntakeAssistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AiAssist(
        [FromBody] IntakeAssistRequest request,
        CancellationToken ct)
    {
        var result = await _assistService.AssistAsync(request, ct);
        return Ok(result);
    }
}

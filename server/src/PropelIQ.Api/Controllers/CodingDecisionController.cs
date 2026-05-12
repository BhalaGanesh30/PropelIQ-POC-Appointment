using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Coding decision workflow API (US_051, US_052).
///
/// Endpoints:
///   POST  /api/v1/coding-decisions/{id}/accept  — Accept AI suggestion as-is (AC-1).
///   PATCH /api/v1/coding-decisions/{id}/modify  — Modify AI suggestion (AC-2).
///   POST  /api/v1/coding-decisions/{id}/reject  — Reject AI suggestion (AC-3).
///   GET   /api/v1/patients/{patientId}/coding-decisions/pending — Pending decisions for AC-4 block.
///   POST  /api/v1/coding-decisions/manual — Manual code selection without AI pipeline (US_052, AC-2).
///
/// All mutation endpoints:
///   - Require Clinician role.
///   - Return HTTP 409 when the encounter is already submitted (Edge Case 1) or the
///     decision has already been acted on.
///   - Return HTTP 422 when the decision ID is invalid (cannot be parsed as GUID).
/// </summary>
[Authorize(Roles = "Clinician")]
[Route("api/v1")]
public sealed class CodingDecisionController : BaseApiController
{
    private readonly ICodingDecisionWorkflowService _workflowService;
    private readonly ICodingDecisionRepository      _decisionRepo;
    private readonly IAuditService                  _auditService;

    public CodingDecisionController(
        ICodingDecisionWorkflowService workflowService,
        ICodingDecisionRepository      decisionRepo,
        IAuditService                  auditService)
    {
        _workflowService = workflowService;
        _decisionRepo    = decisionRepo;
        _auditService    = auditService;
    }

    /// <summary>
    /// Records that the authenticated clinician accepted the AI-suggested code as-is (US_051 AC-1).
    ///
    /// The reviewer identity is sourced from the JWT sub claim; no request body is required.
    ///
    /// Edge Cases:
    ///   - 409 when encounter is already submitted for billing.
    ///   - 409 when decision has already been accepted, modified, or rejected.
    ///   - 404 when the decision ID does not exist.
    /// </summary>
    /// <param name="id">GUID of the coding decision to accept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Decision accepted; audit record written.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    /// <response code="404">Coding decision not found.</response>
    /// <response code="409">Encounter already submitted or decision already decided.</response>
    [HttpPost("coding-decisions/{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptDecision(
        [FromRoute] Guid id,
        [FromQuery] string? reviewerNote,
        CancellationToken ct)
    {
        var reviewerId = TryGetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            await _workflowService.AcceptAsync(id, reviewerId.Value, reviewerNote, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }
        catch (EncounterAlreadySubmittedException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Encounter Already Submitted",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Decision Already Made",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
    }

    /// <summary>
    /// Records that the authenticated clinician modified the AI-suggested code (US_051 AC-2).
    ///
    /// Validates that FinalCode is non-empty (max 20 chars). The original AI-suggested code
    /// is snapshotted in <c>original_icd10_code</c> / <c>original_cpt_code</c> for AIR-007
    /// agreement rate tracking.
    ///
    /// Edge Cases:
    ///   - 400 when FinalCode is empty or exceeds 20 characters.
    ///   - 409 when encounter is already submitted for billing.
    ///   - 409 when decision has already been acted on.
    ///   - 404 when the decision ID does not exist.
    /// </summary>
    /// <param name="id">GUID of the coding decision to modify.</param>
    /// <param name="request">Clinician-supplied final code and description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Decision modified; audit record written with original and final values.</response>
    /// <response code="400">FinalCode is empty or exceeds 20 characters.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    /// <response code="404">Coding decision not found.</response>
    /// <response code="409">Encounter already submitted or decision already decided.</response>
    [HttpPatch("coding-decisions/{id:guid}/modify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ModifyDecision(
        [FromRoute] Guid id,
        [FromBody] ModifyDecisionRequestDto request,
        CancellationToken ct)
    {
        var reviewerId = TryGetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _workflowService.ModifyAsync(id, request, reviewerId.Value, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }
        catch (EncounterAlreadySubmittedException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Encounter Already Submitted",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Decision Already Made",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
    }

    /// <summary>
    /// Records that the authenticated clinician rejected the AI suggestion (US_051 AC-3).
    ///
    /// No request body is required. An audit record with event_type = "coding_rejected" is written.
    ///
    /// Edge Cases:
    ///   - 409 when encounter is already submitted for billing.
    ///   - 409 when decision has already been acted on.
    ///   - 404 when the decision ID does not exist.
    /// </summary>
    /// <param name="id">GUID of the coding decision to reject.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Decision rejected; audit record written.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    /// <response code="404">Coding decision not found.</response>
    /// <response code="409">Encounter already submitted or decision already decided.</response>
    [HttpPost("coding-decisions/{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectDecision(
        [FromRoute] Guid id,
        [FromQuery] string? reviewerNote,
        CancellationToken ct)
    {
        var reviewerId = TryGetCurrentUserId();
        if (reviewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            await _workflowService.RejectAsync(id, reviewerId.Value, reviewerNote, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }
        catch (EncounterAlreadySubmittedException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Encounter Already Submitted",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Decision Already Made",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
            });
        }
    }

    /// <summary>
    /// Returns all pending coding decisions for the given patient (US_051 AC-4).
    ///
    /// Used by the FE "Coding decisions required" submission block banner to list
    /// the specific codes that must be resolved before billing submission is allowed.
    /// </summary>
    /// <param name="patientId">GUID of the patient.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">List of pending decisions (may be empty when all are decided).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    [HttpGet("patients/{patientId:guid}/coding-decisions/pending")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingDecisionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingDecisions(
        [FromRoute] Guid patientId,
        CancellationToken ct)
    {
        var result = await _workflowService.GetPendingAsync(patientId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Records a clinician-initiated manual code selection against the patient's coding record (US_052, AC-2).
    ///
    /// This endpoint bypasses the AI suggestion pipeline — the code is selected directly by
    /// the clinician from the code search results. A <c>coding_decisions</c> row is inserted
    /// with <c>reviewer_action = accepted</c> to indicate the decision is immediately finalized.
    ///
    /// An audit record with <c>event_type = "coding_manual_selected"</c> is written for NFR-010.
    /// </summary>
    /// <param name="request">Patient ID, selected code, code type, and description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Decision created; returns the new decisionId.</response>
    /// <response code="400">Validation failure (missing required fields).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    [HttpPost("coding-decisions/manual")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateManualDecision(
        [FromBody] ManualCodeSelectionRequestDto request,
        CancellationToken ct)
    {
        var clinicianId = TryGetCurrentUserId();
        if (clinicianId is null)
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Insert as an immediately-accepted decision (not pending AI review).
        var decision = new CodingDecision
        {
            PatientId      = request.PatientId,
            DocumentId     = null,
            CodeType       = request.CodeType,
            SuggestedCode  = request.Code,
            FinalizedCode  = request.Code,
            Rationale      = request.Description,
            ConfidenceScore = 1.0m,
            ReviewerAction  = ReviewerAction.Accepted,
            ReviewedByUserId = clinicianId.Value,
            DecidedAt        = DateTimeOffset.UtcNow,
        };

        var ids = await _decisionRepo.InsertPendingAsync([decision], ct);
        var decisionId = ids[0];

        // Append-only audit record (NFR-010, DR-005).
        await _auditService.LogEventAsync(
            eventType:        "coding_manual_selected",
            actorUserId:      clinicianId.Value,
            targetEntityId:   decisionId,
            targetEntityType: "coding_decision",
            metadata: new Dictionary<string, string>
            {
                ["patientId"]   = request.PatientId.ToString(),
                ["code"]        = request.Code,
                ["codeType"]    = request.CodeType,
                ["description"] = request.Description,
            },
            ct);

        DiagnosticsConfig.AcceptDecisionCounter.Add(1,
            new("decision.source", "manual"),
            new("code.type", request.CodeType));

        return StatusCode(StatusCodes.Status201Created, new { decisionId });
    }
}

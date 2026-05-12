using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Exceptions;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// ICD-10 coding suggestion API (US_049).
///
/// GET /api/v1/patients/{id}/coding-suggestions
///
/// Returns up to 3 AI-generated ICD-10 coding suggestions for the given patient.
/// The suggestions are ranked by confidence (descending) and include clinical fact
/// citations that support each code recommendation.
///
/// Access control: the authenticated user must be requesting suggestions for their
/// own patient record (ownership enforced via JWT NameIdentifier claim).
/// All endpoints require JWT bearer authentication.
/// </summary>
[Authorize]
[Route("api/v1/patients")]
public sealed class CodingSuggestionController : BaseApiController
{
    private readonly ICodingSuggestionOrchestrator _orchestrator;

    public CodingSuggestionController(ICodingSuggestionOrchestrator orchestrator)
        => _orchestrator = orchestrator;

    /// <summary>
    /// Generates up to 3 ranked ICD-10 coding suggestions for the specified patient (US_049 AC-1).
    ///
    /// - Suggestions are derived via a RAG pipeline: pgvector evidence retrieval → GPT-4.1 inference.
    /// - <c>LowConfidence=true</c> signals that the top suggestion is below the configured threshold (AC-4).
    /// - <c>InsufficientEvidence=true</c> signals that fewer than 3 suggestions were returned (AC-5).
    /// - Each suggestion includes clinical fact citations used as LLM context (AC-3).
    ///
    /// Edge Cases:
    ///   - 422 when the patient has no extracted clinical facts.
    ///   - 401 when the patient requests another patient's suggestions.
    ///   - 500 (circuit-breaker active) returns 503 via middleware.
    /// </summary>
    /// <param name="id">Patient GUID from the route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Up to 3 ranked ICD-10 suggestions (may include low-confidence flag).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not own this patient record.</response>
    /// <response code="422">Patient has no extracted clinical facts — AI pipeline cannot run.</response>
    [HttpGet("{id:guid}/coding-suggestions")]
    [ProducesResponseType(typeof(CodingSuggestionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCodingSuggestions(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        // Enforce patient ownership — authenticated user may only view their own suggestions.
        var currentUserId = TryGetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        if (currentUserId.Value != id)
        {
            return Forbid();
        }

        try
        {
            var response = await _orchestrator.GenerateSuggestionsAsync(id, currentUserId.Value, ct);
            return Ok(response);
        }
        catch (InsufficientClinicalDataException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Insufficient Clinical Data",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity,
            });
        }
    }
}

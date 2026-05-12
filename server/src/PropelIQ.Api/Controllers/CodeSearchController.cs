using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Code search and favorites management API (US_052, FR-MC-004 [DETERMINISTIC]).
///
/// Endpoints:
///   GET    /api/v1/codes/search?q=&amp;type=all|icd10|cpt&amp;includeDeprecated=false&amp;limit=20
///            — trigram similarity search; favorites pinned first; Redis 60s cache (AC-1, AC-3).
///   GET    /api/v1/users/me/code-favorites
///            — returns authenticated clinician's full favorites list.
///   POST   /api/v1/users/me/code-favorites
///            — adds a code to favorites; HTTP 422 if code not in reference catalog (AC-3).
///   DELETE /api/v1/users/me/code-favorites/{codeType}/{code}
///            — removes a code from favorites; HTTP 404 if not favorited (AC-4).
///
/// All endpoints require Clinician role.
/// </summary>
[Authorize(Roles = "Clinician")]
[Route("api/v1")]
public sealed class CodeSearchController : BaseApiController
{
    private readonly ICodeSearchService _searchService;

    public CodeSearchController(ICodeSearchService searchService)
        => _searchService = searchService;

    /// <summary>
    /// Searches for ICD-10 and/or CPT codes using PostgreSQL pg_trgm trigram similarity (AC-1).
    ///
    /// - Results are sorted by similarity score descending; favorites pinned first (AC-3).
    /// - Deprecated codes excluded by default; pass <c>includeDeprecated=true</c> to override (Edge Case 2).
    /// - Edge Case 1: Zero results → HTTP 200 <c>{ results: [], totalCount: 0 }</c>.
    /// - NFR-002: p95 ≤ 500ms — backed by GIN trigram indexes and Redis 60s cache.
    /// </summary>
    /// <param name="q">Search term — minimum 2 characters required.</param>
    /// <param name="type">Code type filter: "all" (default), "icd10", or "cpt".</param>
    /// <param name="includeDeprecated">When <c>true</c>, deprecated codes are included.</param>
    /// <param name="limit">Maximum results to return (default 20, max 200).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Search results (may be empty — never 404 for no results).</response>
    /// <response code="400">Query term is shorter than 2 characters or type is invalid.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    [HttpGet("codes/search")]
    [ProducesResponseType(typeof(CodeSearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchCodes(
        [FromQuery] string q = "",
        [FromQuery] string type = "all",
        [FromQuery] bool includeDeprecated = false,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (q.Length < 2)
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Invalid Query",
                Detail = "Search term must be at least 2 characters.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (type is not ("all" or "icd10" or "cpt"))
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Invalid Type",
                Detail = "type must be 'all', 'icd10', or 'cpt'.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var userId = TryGetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        using var activity = DiagnosticsConfig.ActivitySource.StartActivity("code_search.query");
        activity?.SetTag("query.type", type);
        activity?.SetTag("query.include_deprecated", includeDeprecated);

        var sw = Stopwatch.StartNew();
        var response = await _searchService.SearchAsync(q, type, includeDeprecated, userId.Value, limit, ct);
        sw.Stop();

        DiagnosticsConfig.CodeSearchDurationHistogram.Record(sw.ElapsedMilliseconds,
            new("query.type", type),
            new("results.count", response.TotalCount));

        return Ok(response);
    }

    /// <summary>
    /// Returns all codes favorited by the authenticated clinician.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">List of favorited codes (may be empty).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    [HttpGet("users/me/code-favorites")]
    [ProducesResponseType(typeof(IReadOnlyList<CodeResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFavorites(CancellationToken ct)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var favorites = await _searchService.GetFavoritesAsync(userId.Value, ct);
        return Ok(favorites);
    }

    /// <summary>
    /// Adds a code to the authenticated clinician's favorites (AC-3).
    ///
    /// Returns HTTP 422 when the code does not exist in the reference catalog.
    /// Idempotent — adding an already-favorited code returns HTTP 201.
    /// </summary>
    /// <param name="request">Code and type to favorite.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Favorite added successfully.</response>
    /// <response code="400">Validation failure (CodeType invalid, Code exceeds 20 chars).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    /// <response code="422">Code does not exist in the reference catalog.</response>
    [HttpPost("users/me/code-favorites")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddFavorite(
        [FromBody] AddFavoriteRequestDto request,
        CancellationToken ct)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var added = await _searchService.AddFavoriteAsync(userId.Value, request.Code, request.CodeType, ct);

        if (!added)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Code Not Found",
                Detail = $"Code '{request.Code}' of type '{request.CodeType}' does not exist in the reference catalog.",
                Status = StatusCodes.Status422UnprocessableEntity,
            });
        }

        DiagnosticsConfig.CodeFavoriteAddCounter.Add(1,
            new KeyValuePair<string, object?>("code.type", request.CodeType));

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Removes a code from the authenticated clinician's favorites (AC-4).
    ///
    /// Returns HTTP 404 when the code is not in the user's favorites.
    /// </summary>
    /// <param name="codeType">Code type: "icd10" or "cpt".</param>
    /// <param name="code">Code value to remove from favorites.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Favorite removed successfully.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Authenticated user does not have the Clinician role.</response>
    /// <response code="404">Code is not in the user's favorites.</response>
    [HttpDelete("users/me/code-favorites/{codeType}/{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(
        [FromRoute] string codeType,
        [FromRoute] string code,
        CancellationToken ct)
    {
        var userId = TryGetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var removed = await _searchService.RemoveFavoriteAsync(userId.Value, codeType, code, ct);

        if (!removed)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Favorite Not Found",
                Detail = $"Code '{code}' of type '{codeType}' is not in your favorites.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        DiagnosticsConfig.CodeFavoriteRemoveCounter.Add(1,
            new KeyValuePair<string, object?>("code.type", codeType));

        return NoContent();
    }
}

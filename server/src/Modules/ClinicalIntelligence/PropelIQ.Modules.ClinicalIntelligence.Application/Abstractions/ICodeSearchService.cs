using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Service abstraction for code search and favorites management (US_052, FR-MC-004 [DETERMINISTIC]).
///
/// Search results are cached in Redis (60-second TTL per unique query+type+deprecated combination).
/// Favorites are merged per-user at query time — favorites appear first in results (AC-3).
/// </summary>
public interface ICodeSearchService
{
    /// <summary>
    /// Searches for matching codes using PostgreSQL pg_trgm trigram similarity.
    ///
    /// Caches the base result set (sans per-user favorites flags) in Redis for 60 seconds.
    /// Favorites are pinned to the top of the response per user at merge time.
    ///
    /// Edge Case 1: Returns HTTP 200 with <c>{ results: [], totalCount: 0 }</c> on no match.
    /// Edge Case 2: Deprecated codes excluded by default; pass <paramref name="includeDeprecated"/> = true to override.
    /// </summary>
    /// <param name="query">Search term — at least 2 characters (enforced by controller).</param>
    /// <param name="type">Code type filter: "all", "icd10", or "cpt".</param>
    /// <param name="includeDeprecated">When <c>true</c>, deprecated codes are included in results.</param>
    /// <param name="userId">Authenticated clinician's user ID — used to resolve per-user favorites.</param>
    /// <param name="limit">Maximum number of results (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CodeSearchResponseDto> SearchAsync(
        string query,
        string type,
        bool includeDeprecated,
        Guid userId,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all codes favorited by <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<CodeResultDto>> GetFavoritesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a code to <paramref name="userId"/>'s favorites.
    ///
    /// Returns <c>false</c> when the code does not exist in the reference catalog
    /// (caller maps to HTTP 422).
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="code">Code value to favorite.</param>
    /// <param name="codeType">Code type: "icd10" or "cpt".</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> AddFavoriteAsync(Guid userId, string code, string codeType, CancellationToken ct = default);

    /// <summary>
    /// Removes a code from <paramref name="userId"/>'s favorites.
    ///
    /// Returns <c>false</c> when the favorite row does not exist (caller maps to HTTP 404).
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="codeType">Code type: "icd10" or "cpt".</param>
    /// <param name="code">Code value to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> RemoveFavoriteAsync(Guid userId, string codeType, string code, CancellationToken ct = default);
}

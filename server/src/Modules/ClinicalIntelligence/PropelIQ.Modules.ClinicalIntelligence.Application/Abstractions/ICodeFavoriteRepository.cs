using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for CRUD operations on <c>app.user_code_favorites</c> (US_052).
///
/// All write operations validate that the referenced code exists in the appropriate
/// reference table (<c>icd_codes</c> or <c>cpt_codes</c>) before persisting.
/// </summary>
public interface ICodeFavoriteRepository
{
    /// <summary>
    /// Returns all favorited codes for <paramref name="userId"/>, joined with the
    /// appropriate reference table (<c>icd_codes</c> or <c>cpt_codes</c>) to resolve descriptions.
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<CodeResultDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns a set of code keys ("{codeType}:{code}") that the user has favorited.
    /// Used by <c>CodeSearchService</c> to mark <c>IsFavorited</c> on search results efficiently.
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<HashSet<string>> GetFavoriteKeysAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new favorite row for <paramref name="userId"/>.
    ///
    /// Returns <c>false</c> when the code does not exist in the reference table
    /// (caller maps to HTTP 422 Unprocessable Entity).
    /// Returns <c>true</c> on success (idempotent — if row already exists, returns <c>true</c>).
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="code">Code value to favorite.</param>
    /// <param name="codeType">Code type: "icd10" or "cpt".</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> AddAsync(Guid userId, string code, string codeType, CancellationToken ct = default);

    /// <summary>
    /// Removes a favorite row for <paramref name="userId"/>.
    ///
    /// Returns <c>false</c> when the row does not exist (caller maps to HTTP 404).
    /// Returns <c>true</c> on successful deletion (AC-4).
    /// </summary>
    /// <param name="userId">Authenticated clinician's user ID.</param>
    /// <param name="codeType">Code type: "icd10" or "cpt".</param>
    /// <param name="code">Code value to remove from favorites.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> RemoveAsync(Guid userId, string codeType, string code, CancellationToken ct = default);
}

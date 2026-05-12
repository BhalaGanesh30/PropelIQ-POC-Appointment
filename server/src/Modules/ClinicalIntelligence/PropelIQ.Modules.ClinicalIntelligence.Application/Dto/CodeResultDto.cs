namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Represents a single code result from a code search query (US_052, FR-MC-004).
/// Declared as a record to support non-destructive mutation via <c>with</c> expressions
/// (used by CodeSearchService to set per-user IsFavorited flag without re-allocating).
/// Used in <see cref="CodeSearchResponseDto"/>.
/// </summary>
/// <param name="Code">The code value, e.g. "E11.9" or "99213".</param>
/// <param name="Description">Human-readable description of the code.</param>
/// <param name="CodeType">Code type discriminator: "icd10" or "cpt".</param>
/// <param name="IsDeprecated"><c>true</c> when this code is retired from the active catalog.</param>
/// <param name="IsFavorited">
/// <c>true</c> when this code is in the authenticated clinician's personal favorites.
/// Favorited codes are pinned to the top of search results (AC-3).
/// </param>
public sealed record CodeResultDto(
    string Code,
    string Description,
    string CodeType,
    bool IsDeprecated,
    bool IsFavorited);

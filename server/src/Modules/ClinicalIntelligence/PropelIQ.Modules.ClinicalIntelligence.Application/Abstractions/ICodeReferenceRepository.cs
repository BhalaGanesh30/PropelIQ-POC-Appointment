using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for raw-SQL trigram similarity search across
/// the <c>icd_codes</c> and <c>cpt_codes</c> reference catalogs (US_052, FR-MC-004).
///
/// Executes a PostgreSQL UNION with <c>pg_trgm</c> similarity scoring.
/// GIN indexes on <c>code</c> and <c>description</c> are required (task_003) for NFR-002 ≤ 500ms p95.
/// </summary>
public interface ICodeReferenceRepository
{
    /// <summary>
    /// Returns code candidates from <c>icd_codes</c> and/or <c>cpt_codes</c> scored by
    /// trigram similarity with <paramref name="query"/>, sorted by score descending.
    ///
    /// <paramref name="type"/> filters the UNION branches:
    ///   - "icd10"   → only <c>icd_codes</c>
    ///   - "cpt"     → only <c>cpt_codes</c>
    ///   - "all"     → both branches (default)
    ///
    /// When <paramref name="includeDeprecated"/> is <c>false</c> (default),
    /// rows with <c>is_deprecated = true</c> are excluded (Edge Case 2).
    /// </summary>
    /// <param name="query">Search term (at least 2 characters — validated by controller).</param>
    /// <param name="type">Code type filter: "all", "icd10", or "cpt".</param>
    /// <param name="includeDeprecated">When <c>true</c>, deprecated codes are included.</param>
    /// <param name="limit">Maximum number of results to return (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<CodeResultDto>> SearchAsync(
        string query,
        string type,
        bool includeDeprecated,
        int limit,
        CancellationToken ct = default);
}

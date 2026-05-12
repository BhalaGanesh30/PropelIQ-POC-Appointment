using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICodeReferenceRepository"/>.
///
/// Executes a raw SQL UNION across <c>app.icd_codes</c> and <c>app.cpt_codes</c>,
/// scoring rows by <c>pg_trgm</c> trigram similarity against the search term.
/// Relies on GIN indexes created by task_003 for NFR-002 ≤ 500ms p95 (AC-1).
///
/// Parameters are always passed as interpolated values — EF Core renders them
/// as positional placeholders ($1, $2 …) preventing SQL injection.
/// </summary>
internal sealed class CodeReferenceRepository : ICodeReferenceRepository
{
    private readonly AppDbContext _db;

    public CodeReferenceRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodeResultDto>> SearchAsync(
        string query,
        string type,
        bool includeDeprecated,
        int limit,
        CancellationToken ct = default)
    {
        // Build a LIKE pattern for broad candidate recall before pg_trgm scoring.
        var likePattern = $"%{query}%";

        // Clamp limit to avoid runaway queries.
        var effectiveLimit = Math.Min(limit, 200);

        var results = type switch
        {
            "icd10"  => await SearchIcdOnlyAsync(query, likePattern, includeDeprecated, effectiveLimit, ct),
            "cpt"    => await SearchCptOnlyAsync(query, likePattern, includeDeprecated, effectiveLimit, ct),
            _        => await SearchAllAsync(query, likePattern, includeDeprecated, effectiveLimit, ct),
        };

        return results;
    }

    private async Task<IReadOnlyList<CodeResultDto>> SearchIcdOnlyAsync(
        string query,
        string likePattern,
        bool includeDeprecated,
        int limit,
        CancellationToken ct)
    {
        if (includeDeprecated)
        {
            return await _db.Database.SqlQuery<CodeSearchRawResult>($"""
                SELECT
                    code,
                    description,
                    'icd10' AS code_type,
                    is_deprecated,
                    similarity(code || ' ' || description, {query}) AS score
                FROM app.icd_codes
                WHERE (code ILIKE {likePattern} OR description ILIKE {likePattern})
                ORDER BY score DESC
                LIMIT {limit}
                """).Select(r => MapToDto(r)).ToListAsync(ct);
        }

        return await _db.Database.SqlQuery<CodeSearchRawResult>($"""
            SELECT
                code,
                description,
                'icd10' AS code_type,
                is_deprecated,
                similarity(code || ' ' || description, {query}) AS score
            FROM app.icd_codes
            WHERE (code ILIKE {likePattern} OR description ILIKE {likePattern})
              AND is_deprecated = false
            ORDER BY score DESC
            LIMIT {limit}
            """).Select(r => MapToDto(r)).ToListAsync(ct);
    }

    private async Task<IReadOnlyList<CodeResultDto>> SearchCptOnlyAsync(
        string query,
        string likePattern,
        bool includeDeprecated,
        int limit,
        CancellationToken ct)
    {
        if (includeDeprecated)
        {
            return await _db.Database.SqlQuery<CodeSearchRawResult>($"""
                SELECT
                    cpt_code AS code,
                    description,
                    'cpt' AS code_type,
                    is_deprecated,
                    similarity(cpt_code || ' ' || description, {query}) AS score
                FROM app.cpt_codes
                WHERE (cpt_code ILIKE {likePattern} OR description ILIKE {likePattern})
                ORDER BY score DESC
                LIMIT {limit}
                """).Select(r => MapToDto(r)).ToListAsync(ct);
        }

        return await _db.Database.SqlQuery<CodeSearchRawResult>($"""
            SELECT
                cpt_code AS code,
                description,
                'cpt' AS code_type,
                is_deprecated,
                similarity(cpt_code || ' ' || description, {query}) AS score
            FROM app.cpt_codes
            WHERE (cpt_code ILIKE {likePattern} OR description ILIKE {likePattern})
              AND is_deprecated = false
            ORDER BY score DESC
            LIMIT {limit}
            """).Select(r => MapToDto(r)).ToListAsync(ct);
    }

    private async Task<IReadOnlyList<CodeResultDto>> SearchAllAsync(
        string query,
        string likePattern,
        bool includeDeprecated,
        int limit,
        CancellationToken ct)
    {
        if (includeDeprecated)
        {
            return await _db.Database.SqlQuery<CodeSearchRawResult>($"""
                SELECT
                    code,
                    description,
                    'icd10' AS code_type,
                    is_deprecated,
                    similarity(code || ' ' || description, {query}) AS score
                FROM app.icd_codes
                WHERE (code ILIKE {likePattern} OR description ILIKE {likePattern})
                UNION ALL
                SELECT
                    cpt_code AS code,
                    description,
                    'cpt' AS code_type,
                    is_deprecated,
                    similarity(cpt_code || ' ' || description, {query}) AS score
                FROM app.cpt_codes
                WHERE (cpt_code ILIKE {likePattern} OR description ILIKE {likePattern})
                ORDER BY score DESC
                LIMIT {limit}
                """).Select(r => MapToDto(r)).ToListAsync(ct);
        }

        return await _db.Database.SqlQuery<CodeSearchRawResult>($"""
            SELECT
                code,
                description,
                'icd10' AS code_type,
                is_deprecated,
                similarity(code || ' ' || description, {query}) AS score
            FROM app.icd_codes
            WHERE (code ILIKE {likePattern} OR description ILIKE {likePattern})
              AND is_deprecated = false
            UNION ALL
            SELECT
                cpt_code AS code,
                description,
                'cpt' AS code_type,
                is_deprecated,
                similarity(cpt_code || ' ' || description, {query}) AS score
            FROM app.cpt_codes
            WHERE (cpt_code ILIKE {likePattern} OR description ILIKE {likePattern})
              AND is_deprecated = false
            ORDER BY score DESC
            LIMIT {limit}
            """).Select(r => MapToDto(r)).ToListAsync(ct);
    }

    private static CodeResultDto MapToDto(CodeSearchRawResult r) =>
        new(r.Code, r.Description, r.CodeType, r.IsDeprecated, false /* Populated by CodeSearchService at merge time */);

    /// <summary>
    /// Private projection type used by <c>Database.SqlQuery&lt;T&gt;</c>.
    /// EF Core 7+ requires a parameterless constructor and public settable properties
    /// for arbitrary SQL result projection.
    /// </summary>
    private sealed class CodeSearchRawResult
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CodeType { get; set; } = string.Empty;
        public bool IsDeprecated { get; set; }
        public double Score { get; set; }
    }
}

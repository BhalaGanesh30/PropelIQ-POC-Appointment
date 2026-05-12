using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ICodeSearchService"/> (US_052, FR-MC-004 [DETERMINISTIC]).
///
/// Search strategy:
///   1. Compute a cache key from (q, type, includeDeprecated) — shared across users.
///   2. On Redis hit → return base results; merge per-user favorites at response time.
///   3. On Redis miss → call <see cref="ICodeReferenceRepository.SearchAsync"/> → cache 60s.
///   4. Fetch user's favorite keys → mark <c>IsFavorited</c> → pin favorites first.
///
/// Favorites are merged at query time (not cached per-user) so the shared cache
/// remains small and user-agnostic.
/// </summary>
internal sealed class CodeSearchService : ICodeSearchService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ICodeReferenceRepository _referenceRepo;
    private readonly ICodeFavoriteRepository  _favoriteRepo;
    private readonly ICacheService            _cache;
    private readonly ILogger<CodeSearchService> _logger;

    public CodeSearchService(
        ICodeReferenceRepository referenceRepo,
        ICodeFavoriteRepository  favoriteRepo,
        ICacheService            cache,
        ILogger<CodeSearchService> logger)
    {
        _referenceRepo = referenceRepo;
        _favoriteRepo  = favoriteRepo;
        _cache         = cache;
        _logger        = logger;
    }

    /// <inheritdoc />
    public async Task<CodeSearchResponseDto> SearchAsync(
        string query,
        string type,
        bool includeDeprecated,
        Guid userId,
        int limit,
        CancellationToken ct = default)
    {
        var cacheKey = BuildSearchCacheKey(query, type, includeDeprecated);

        // ── 1. Cache read (base result — no per-user favorites flag) ──────────
        var baseResults = await _cache.GetAsync<List<CodeResultDto>>(cacheKey, ct);

        if (baseResults is null)
        {
            _logger.LogDebug(
                "Code search cache miss — q={Query} type={Type} includeDeprecated={IncludeDeprecated}",
                query, type, includeDeprecated);

            var searchResults = await _referenceRepo.SearchAsync(query, type, includeDeprecated, limit, ct);
            baseResults = [..searchResults];

            await _cache.SetAsync(cacheKey, baseResults, CacheTtl, ct);

            DiagnosticsConfig.CodeSearchMissCounter.Add(1,
                new KeyValuePair<string, object?>("query.type", type));
        }
        else
        {
            DiagnosticsConfig.CodeSearchHitCounter.Add(1,
                new KeyValuePair<string, object?>("query.type", type));
        }

        // ── 2. Fetch per-user favorites for IsFavorited flag + pinning ────────
        var favoriteKeys = await _favoriteRepo.GetFavoriteKeysAsync(userId, ct);

        // ── 3. Enrich results with per-user IsFavorited, then pin favorites ───
        var enriched = baseResults
            .Select(r => r with { IsFavorited = favoriteKeys.Contains($"{r.CodeType}:{r.Code}") })
            .ToList();

        var favorites    = enriched.Where(r => r.IsFavorited).ToList();
        var nonFavorites = enriched.Where(r => !r.IsFavorited).ToList();

        var ordered = favorites.Concat(nonFavorites).ToList();

        // Record query duration metric via the caller (controller emits histogram).
        return new CodeSearchResponseDto
        {
            Results    = ordered,
            TotalCount = ordered.Count,
        };
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CodeResultDto>> GetFavoritesAsync(Guid userId, CancellationToken ct = default)
        => _favoriteRepo.GetByUserAsync(userId, ct);

    /// <inheritdoc />
    public Task<bool> AddFavoriteAsync(Guid userId, string code, string codeType, CancellationToken ct = default)
        => _favoriteRepo.AddAsync(userId, code, codeType, ct);

    /// <inheritdoc />
    public Task<bool> RemoveFavoriteAsync(Guid userId, string codeType, string code, CancellationToken ct = default)
        => _favoriteRepo.RemoveAsync(userId, codeType, code, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a deterministic Redis key shared across all users for the same query parameters.
    /// Uses SHA-256 of the composite key to keep key length bounded.
    /// </summary>
    private static string BuildSearchCacheKey(string query, string type, bool includeDeprecated)
    {
        var raw = $"{query}|{type}|{includeDeprecated}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hash = Convert.ToHexString(hashBytes)[..16]; // First 16 hex chars = 64 bits.
        return $"codes:search:{hash}";
    }
}

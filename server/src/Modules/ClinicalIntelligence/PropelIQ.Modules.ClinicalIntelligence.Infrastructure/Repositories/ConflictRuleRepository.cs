using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IConflictRuleRepository"/>.
///
/// Caches active rules in-process for 5 minutes using <see cref="IMemoryCache"/> because
/// rules change infrequently (typically only during rule-base updates, not per-request).
/// This avoids repeated database round-trips on each conflict detection call.
/// </summary>
public sealed class ConflictRuleRepository : IConflictRuleRepository
{
    private const string ActiveRulesCacheKey  = "conflict_rules:active";
    private const string LastUpdatedCacheKey  = "conflict_rules:last_updated";
    private static readonly TimeSpan RuleCacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _memoryCache;

    public ConflictRuleRepository(AppDbContext db, IMemoryCache memoryCache)
    {
        _db = db;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConflictRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<ConflictRule>>(ActiveRulesCacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        var rules = await _db.ConflictRules
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        _memoryCache.Set(ActiveRulesCacheKey, (IReadOnlyList<ConflictRule>)rules, RuleCacheTtl);
        return rules;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken ct = default)
    {
        if (_memoryCache.TryGetValue<DateTimeOffset?>(LastUpdatedCacheKey, out var cached))
        {
            return cached;
        }

        var lastUpdated = await _db.ConflictRules
            .OrderByDescending(r => r.LastUpdatedAt)
            .Select(r => (DateTimeOffset?)r.LastUpdatedAt)
            .FirstOrDefaultAsync(ct);

        _memoryCache.Set(LastUpdatedCacheKey, lastUpdated, RuleCacheTtl);
        return lastUpdated;
    }
}

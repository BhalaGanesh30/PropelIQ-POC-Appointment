using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// CPT catalog freshness service (US_050, Edge Case 2).
///
/// Queries <c>MAX(last_updated_at)</c> from <c>app.cpt_codes</c> and compares
/// against the configured threshold (default 90 days).  The result is cached in
/// Redis for 1 hour to avoid repeated DB scans per request.
/// </summary>
internal sealed class CptCodeFreshnessService : ICptCodeFreshnessService
{
    private const string CacheKey = "cpt-freshness";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly ICptCodeRepository _repo;
    private readonly ICacheService _cache;
    private readonly int _maxAgeDays;
    private readonly ILogger<CptCodeFreshnessService> _logger;

    public CptCodeFreshnessService(
        ICptCodeRepository repo,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<CptCodeFreshnessService> logger)
    {
        _repo   = repo;
        _cache  = cache;
        _logger = logger;
        _maxAgeDays = int.TryParse(configuration["CPT:MaxAgeDays"], out var days) ? days : 90;
    }

    /// <inheritdoc />
    public async Task<CptFreshnessResult> CheckFreshnessAsync(CancellationToken ct = default)
    {
        // Cache check — avoids repeated DB scan per request burst.
        var cached = await _cache.GetAsync<CptFreshnessResult>(CacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var lastUpdated = await _repo.GetLastUpdatedAtAsync(ct);

        bool isStale;
        if (lastUpdated is null)
        {
            // Empty catalog — always stale.
            _logger.LogWarning("CPT code catalog is empty. Returning stale = true.");
            isStale = true;
        }
        else
        {
            var age = DateTimeOffset.UtcNow - lastUpdated.Value;
            isStale = age.TotalDays > _maxAgeDays;
            if (isStale)
            {
                _logger.LogWarning(
                    "CPT code catalog is stale (last updated {LastUpdated}, age {AgeDays:0} days > threshold {MaxDays}).",
                    lastUpdated.Value,
                    age.TotalDays,
                    _maxAgeDays);
            }
        }

        var result = new CptFreshnessResult { IsStale = isStale, LastUpdatedAt = lastUpdated };
        await _cache.SetAsync(CacheKey, result, CacheTtl, ct);
        return result;
    }
}

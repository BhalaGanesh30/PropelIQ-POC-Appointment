using System.Collections.Concurrent;
using PropelIQ.Modules.SharedServices.Application.Kpi;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Kpi;

/// <summary>
/// In-memory snapshot cache for KPI summary responses (US_060, edge case 1).
///
/// <para>
/// Cache entries older than <see cref="StalenessThreshold"/> (1 hour) are returned with
/// <see cref="KpiSummaryResponse.IsStale"/> = <c>true</c> so the UI can show a staleness warning.
/// The cache is NOT evicted automatically — stale entries remain and are flagged, allowing
/// the UI to show the last-known data while a fresh computation is triggered.
/// </para>
///
/// Registered as a singleton in <c>SharedServicesServiceRegistration</c>.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class KpiSnapshotCacheService
{
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, (KpiSummaryResponse Data, DateTime CachedAt)> _cache
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a cached summary for <paramref name="range"/> if one exists, or <c>null</c>
    /// on cache miss.  Sets <see cref="KpiSummaryResponse.IsStale"/> based on cache age.
    /// </summary>
    public KpiSummaryResponse? TryGet(DateRange range)
    {
        var key = BuildKey(range);
        if (!_cache.TryGetValue(key, out var entry))
            return null;

        var isStale = DateTime.UtcNow - entry.CachedAt > StalenessThreshold;
        return entry.Data with { IsStale = isStale };
    }

    /// <summary>Stores or replaces the cached summary for <paramref name="range"/>.</summary>
    public void Set(DateRange range, KpiSummaryResponse data)
    {
        var key = BuildKey(range);
        _cache[key] = (data, DateTime.UtcNow);
    }

    /// <summary>Removes all cached entries. Call after a bulk metrics refresh.</summary>
    public void Invalidate() => _cache.Clear();

    private static string BuildKey(DateRange range) =>
        $"{range.From:yyyy-MM-dd}_{range.To:yyyy-MM-dd}";
}

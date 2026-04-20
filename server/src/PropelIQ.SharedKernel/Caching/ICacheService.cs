namespace PropelIQ.SharedKernel.Caching;

/// <summary>
/// Distributed cache abstraction used by all modules.
/// Implementations (e.g. RedisCacheService) must never throw on cache misses or
/// Redis unavailability — callers always fall back to the primary data source.
/// TR-004: Distributed cache for hot slot search and profile read acceleration.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value for <paramref name="key"/>, or <c>default</c> on miss or failure.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> with an optional TTL override.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Removes the cache entry for <paramref name="key"/>.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes all cache entries whose key starts with <paramref name="prefix"/>.
    /// Requires direct IConnectionMultiplexer access; deferred to EP-002.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

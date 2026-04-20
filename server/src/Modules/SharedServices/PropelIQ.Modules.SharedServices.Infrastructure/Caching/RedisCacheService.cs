using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using PropelIQ.SharedKernel.Caching;
using StackExchange.Redis;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/> using <see cref="IDistributedCache"/>.
/// Wraps every Redis call in a Polly v8 circuit breaker (via Microsoft.Extensions.Resilience).
/// AC-4: When Redis is unavailable the service logs a warning and returns null (cache miss)
/// so callers transparently fall back to the database — no unhandled exception propagates.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly ResiliencePipeline _pipeline;

    public RedisCacheService(
        IDistributedCache cache,
        IOptions<CacheOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;

        // Circuit breaker: opens after FailureThreshold failures within SamplingWindow seconds.
        // Transitions to half-open after BreakDuration seconds to probe Redis recovery.
        // FailureRatio = 1.0 + MinimumThroughput = FailureThreshold → trips after N consecutive failures.
        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<RedisConnectionException>()
                    .Handle<RedisTimeoutException>()
                    .Handle<RedisException>(),
                FailureRatio = 1.0,
                MinimumThroughput = _options.CircuitBreaker.FailureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(_options.CircuitBreaker.SamplingWindowSeconds),
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreaker.BreakDurationSeconds),
                OnOpened = args =>
                {
                    logger.LogWarning(
                        "Redis circuit breaker opened. Requests will bypass cache for {Duration} seconds.",
                        _options.CircuitBreaker.BreakDurationSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Redis circuit breaker closed. Cache restored.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var json = await _pipeline.ExecuteAsync(
                async token => await _cache.GetStringAsync(key, token),
                ct);

            if (json is null) return default;
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex,
                "Redis circuit open for cache key {CacheKey}. Falling back to database.", key);
            return default;
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or RedisException)
        {
            _logger.LogWarning(ex,
                "Redis unavailable for cache key {CacheKey}. Falling back to database.", key);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds)
            };

            await _pipeline.ExecuteAsync(
                async token => { await _cache.SetStringAsync(key, json, entryOptions, token); },
                ct);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex,
                "Redis circuit open; skipping cache set for key {CacheKey}.", key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or RedisException)
        {
            _logger.LogWarning(ex,
                "Redis unavailable; skipping cache set for key {CacheKey}.", key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _pipeline.ExecuteAsync(
                async token => { await _cache.RemoveAsync(key, token); },
                ct);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex,
                "Redis circuit open; skipping cache remove for key {CacheKey}.", key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or RedisException)
        {
            _logger.LogWarning(ex,
                "Redis unavailable; skipping cache remove for key {CacheKey}.", key);
        }
    }

    /// <inheritdoc/>
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        // IDistributedCache does not expose key scanning.
        // Full prefix-based eviction requires IConnectionMultiplexer with SCAN command.
        // Deferred to EP-002 (booking confirmation invalidation flows).
        _logger.LogWarning(
            "RemoveByPrefixAsync called for prefix {Prefix}. " +
            "Prefix eviction requires IConnectionMultiplexer; deferred to EP-002.",
            prefix);
        return Task.CompletedTask;
    }
}

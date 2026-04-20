using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PropelIQ.Api.Infrastructure.HealthChecks;

/// <summary>
/// Health check that probes Redis availability via a round-trip set/get on a sentinel key.
/// Reports <see cref="HealthStatus.Degraded"/> rather than Unhealthy so that the API host
/// continues to serve requests from the database when Redis is unavailable (AC-4).
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private const string PingKey = "health:redis:ping";
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            };

            await _cache.SetStringAsync(PingKey, "pong", options, cancellationToken);
            var result = await _cache.GetStringAsync(PingKey, cancellationToken);

            return result == "pong"
                ? HealthCheckResult.Healthy("Redis is available and responsive.")
                : HealthCheckResult.Degraded("Redis round-trip set/get returned unexpected value.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                $"Redis unavailable: {ex.Message}", ex);
        }
    }
}

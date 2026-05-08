using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of <see cref="ITimelineCacheService"/> (US_048, TR-004).
///
/// Cache key format: <c>timeline:{patientId}:cat:{category}:from:{dateFrom}:to:{dateTo}</c>
/// TTL: 60 seconds — balances NFR-002 (&lt;500 ms p95) read speed against timeline freshness.
///
/// Delegates all Redis interaction (resilience, circuit breaker, serialization) to the shared
/// <see cref="ICacheService"/> from SharedKernel so Redis failures never surface as exceptions.
/// </summary>
public sealed class TimelineCacheService : ITimelineCacheService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ICacheService _cache;
    private readonly ILogger<TimelineCacheService> _logger;

    public TimelineCacheService(ICacheService cache, ILogger<TimelineCacheService> logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TimelineResponseDto?> GetAsync(
        Guid patientId,
        TimelineQuery query,
        CancellationToken ct = default)
    {
        var result = await _cache.GetAsync<TimelineResponseDto>(BuildKey(patientId, query), ct);
        if (result is not null)
        {
            _logger.LogDebug(
                "Timeline cache HIT for patient {PatientId} key={Key}",
                patientId, BuildKey(patientId, query));
        }
        return result;
    }

    /// <inheritdoc />
    public Task SetAsync(
        Guid patientId,
        TimelineQuery query,
        TimelineResponseDto response,
        CancellationToken ct = default)
        => _cache.SetAsync(BuildKey(patientId, query), response, CacheTtl, ct);

    /// <summary>
    /// Builds a deterministic cache key that encodes all filter dimensions so that
    /// different filter combinations do not share a cache entry (Edge Case 2).
    /// </summary>
    private static string BuildKey(Guid patientId, TimelineQuery query)
    {
        var cat      = query.Category?.Trim().ToLowerInvariant() ?? "all";
        var dateFrom = query.DateFrom?.ToString("yyyyMMdd") ?? "none";
        var dateTo   = query.DateTo?.ToString("yyyyMMdd") ?? "none";
        return $"timeline:{patientId}:cat:{cat}:from:{dateFrom}:to:{dateTo}";
    }
}

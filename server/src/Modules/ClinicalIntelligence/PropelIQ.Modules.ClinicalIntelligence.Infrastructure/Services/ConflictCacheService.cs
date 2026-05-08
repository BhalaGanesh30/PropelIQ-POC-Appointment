using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IConflictCacheService"/> (TR-004).
///
/// Cache key format: <c>conflicts:{patientId}</c>
/// TTL: 30 seconds — short to ensure acknowledged status is promptly reflected.
///
/// Delegates all Redis interaction (resilience, circuit breaker, serialization) to
/// the shared <see cref="ICacheService"/> from SharedKernel so cache failures
/// are handled centrally and never propagate as unhandled exceptions.
/// </summary>
public sealed class ConflictCacheService : IConflictCacheService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly ICacheService _cache;
    private readonly ILogger<ConflictCacheService> _logger;

    public ConflictCacheService(ICacheService cache, ILogger<ConflictCacheService> logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ConflictAlertsResponseDto?> GetAsync(Guid patientId, CancellationToken ct = default)
    {
        var result = await _cache.GetAsync<ConflictAlertsResponseDto>(BuildKey(patientId), ct);
        if (result is not null)
        {
            _logger.LogDebug("Conflict cache HIT for patient {PatientId}", patientId);
        }
        return result;
    }

    /// <inheritdoc />
    public Task SetAsync(Guid patientId, ConflictAlertsResponseDto response, CancellationToken ct = default)
        => _cache.SetAsync(BuildKey(patientId), response, CacheTtl, ct);

    /// <inheritdoc />
    public async Task InvalidateAsync(Guid patientId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(BuildKey(patientId), ct);
        _logger.LogDebug("Conflict cache INVALIDATED for patient {PatientId}", patientId);
    }

    private static string BuildKey(Guid patientId) => $"conflicts:{patientId}";
}

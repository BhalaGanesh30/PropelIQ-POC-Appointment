using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Redis-backed cache for clinical timeline responses (US_048, TR-004).
///
/// Cache key format: <c>timeline:{patientId}:cat:{category}:from:{dateFrom}:to:{dateTo}</c>
/// TTL: 60 seconds — balances NFR-002 read speed against timeline freshness.
/// </summary>
public interface ITimelineCacheService
{
    /// <summary>Returns the cached response, or <c>null</c> on cache miss.</summary>
    Task<TimelineResponseDto?> GetAsync(Guid patientId, TimelineQuery query, CancellationToken ct = default);

    /// <summary>Stores the response with a 60-second TTL.</summary>
    Task SetAsync(Guid patientId, TimelineQuery query, TimelineResponseDto response, CancellationToken ct = default);
}

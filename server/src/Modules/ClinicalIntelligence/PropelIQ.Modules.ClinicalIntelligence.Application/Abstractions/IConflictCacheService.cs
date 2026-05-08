using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Redis-backed cache for conflict detection results (TR-004).
///
/// Cache key: <c>conflicts:{patientId}</c>.
/// TTL: 30 seconds — short because conflict state is sensitive and
/// acknowledged status must reflect promptly.
/// </summary>
public interface IConflictCacheService
{
    /// <summary>Returns the cached response for the patient, or null on cache miss.</summary>
    Task<ConflictAlertsResponseDto?> GetAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>Stores the response with a 30-second TTL.</summary>
    Task SetAsync(Guid patientId, ConflictAlertsResponseDto response, CancellationToken ct = default);

    /// <summary>
    /// Removes the cache entry so the next GET reflects updated acknowledged state.
    /// Called after a successful <c>AcknowledgeAsync</c> write.
    /// </summary>
    Task InvalidateAsync(Guid patientId, CancellationToken ct = default);
}

using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Aggregates clinical facts from all categories into a single patient profile response.
/// Supports partial failure isolation (Edge Case 1) and Redis caching (TR-004, NFR-002).
/// </summary>
public interface IPatientProfileAggregationService
{
    /// <summary>
    /// Returns the full 360° profile for <paramref name="patientId"/>.
    ///
    /// Each category is queried independently — failure in one category results in an
    /// HTTP 200 response with partial data and <see cref="PatientProfileDto.Partial"/> = true
    /// rather than a hard error (Edge Case 1).
    ///
    /// Results are cached in Redis with a 60-second TTL (TR-004, NFR-002).
    /// </summary>
    Task<PatientProfileDto> AggregateProfileAsync(
        Guid patientId,
        ProfileQuery query,
        CancellationToken ct = default);
}

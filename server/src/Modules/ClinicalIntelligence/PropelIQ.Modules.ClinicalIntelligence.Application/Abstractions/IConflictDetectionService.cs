using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Drives the drug-drug and drug-allergy conflict detection pipeline for a patient.
///
/// Loads active facts and rules, normalizes names, performs cross-product evaluation,
/// deduplicates to highest severity per pair (Edge Case 2), checks rule staleness
/// (Edge Case 1), and upserts conflict_alerts rows.
/// </summary>
public interface IConflictDetectionService
{
    /// <summary>
    /// Evaluates conflicts for <paramref name="patientId"/> and returns a sorted, deduplicated
    /// response — cached in Redis for <c>conflicts:{patientId}</c> with a 30-second TTL (TR-004).
    /// </summary>
    /// <param name="patientId">The patient GUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="ConflictAlertsResponseDto"/> sorted Critical → High → Moderate → Low.
    /// </returns>
    Task<ConflictAlertsResponseDto> EvaluateConflictsAsync(
        Guid patientId,
        CancellationToken ct = default);
}

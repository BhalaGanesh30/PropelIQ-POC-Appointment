using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for <see cref="ClinicalFact"/> persistence.
/// Provides bulk insert and query operations needed by the extraction pipeline.
/// </summary>
public interface IClinicalFactRepository
{
    /// <summary>Persists a batch of extracted facts in a single round-trip.</summary>
    Task AddRangeAsync(IEnumerable<ClinicalFact> facts, CancellationToken ct = default);

    /// <summary>Returns all facts associated with the given document (AC-2, AIR-004).</summary>
    Task<IReadOnlyList<ClinicalFact>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>Returns all facts for all documents belonging to the given patient.</summary>
    Task<IReadOnlyList<ClinicalFact>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated page of facts for the given patient, filtered to a single
    /// <paramref name="factType"/> category. Includes the source document via JOIN.
    /// Also returns the total count for that category (to support FE pagination / virtual scroll).
    /// US_045 AC-2, AC-3, Edge Case 2.
    /// </summary>
    Task<(List<ClinicalFact> Facts, int Total)> GetByPatientIdGroupedAsync(
        Guid patientId,
        string factType,
        int limit,
        int offset,
        CancellationToken ct = default);

    // ── US_047 additions ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns a single fact by its primary key, or null when not found.
    /// Includes the source document navigation so the response DTO can be fully populated.
    /// US_047 AC-1 / Edge Case 1.
    /// </summary>
    Task<ClinicalFact?> GetByIdAsync(Guid factId, CancellationToken ct = default);

    /// <summary>
    /// Atomically updates <paramref name="fact"/> only when its current <c>row_version</c>
    /// equals <paramref name="expectedRowVersion"/> (optimistic concurrency guard).
    ///
    /// Uses a raw SQL UPDATE WHERE row_version = @expected pattern so that two concurrent
    /// clinicians editing the same fact at the same time cannot silently overwrite each other.
    ///
    /// Returns <c>true</c> when exactly one row was updated (version matched);
    /// <c>false</c> when zero rows were updated (version mismatch — HTTP 409 caller).
    /// US_047 Edge Case 1, DR-002.
    /// </summary>
    Task<bool> UpdateAsync(ClinicalFact fact, int expectedRowVersion, CancellationToken ct = default);

    // ── US_048 additions ──────────────────────────────────────────────────────

    /// <summary>
    /// Projects facts for <paramref name="patientId"/> to <see cref="TimelineEventDto"/> list
    /// applying optional type and date filters at query time (US_048 AC-2, AC-3, NFR-002).
    ///
    /// When <paramref name="factType"/> is null all fact types are included.
    /// Date bounds are inclusive. Returns an empty list when no facts match (Edge Case 1).
    /// </summary>
    Task<List<TimelineEventDto>> GetTimelineFactsAsync(
        Guid patientId,
        string? factType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);

    // ── US_049 additions ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when at least one clinical fact exists for the patient.
    /// Used as a preflight check before invoking the AI pipeline (US_049 Edge Case 2).
    /// </summary>
    Task<bool> HasFactsAsync(Guid patientId, CancellationToken ct = default);
}


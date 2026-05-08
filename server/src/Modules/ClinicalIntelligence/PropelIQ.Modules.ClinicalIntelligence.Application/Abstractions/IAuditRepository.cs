using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for querying immutable audit records scoped to a clinical entity.
/// Write operations are handled by the shared <c>IAuditService.LogEventAsync</c> to
/// maintain the append-only constraint (NFR-010, DR-005).
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Returns audit records for the given entity ordered chronologically ascending (AC-3).
    ///
    /// Performs a left-join with <c>users</c> to resolve the actor's display name.
    /// Returns only entries whose <c>event_type</c> is <c>fact_edited</c> or <c>fact_verified</c>.
    /// </summary>
    /// <param name="entityType">Discriminator for the entity type, e.g. <c>"clinical_fact"</c>.</param>
    /// <param name="entityId">Primary key of the target entity.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<FactAuditEntryDto>> GetByEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default);
}

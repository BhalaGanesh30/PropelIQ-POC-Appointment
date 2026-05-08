using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository contract for <c>clinical_documents</c> CRUD operations.
/// </summary>
public interface IClinicalDocumentRepository
{
    /// <summary>Persists a new <see cref="ClinicalDocument"/> and returns the saved entity.</summary>
    Task<ClinicalDocument> AddAsync(ClinicalDocument document, CancellationToken ct = default);

    /// <summary>Returns the document with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<ClinicalDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all documents currently in the <c>PendingScan</c> state for retry processing.</summary>
    Task<IReadOnlyList<ClinicalDocument>> GetPendingScanDocumentsAsync(CancellationToken ct = default);

    /// <summary>Returns all documents whose <c>extraction_status</c> is <c>Failed</c>.</summary>
    Task<IReadOnlyList<ClinicalDocument>> GetFailedDocumentsAsync(CancellationToken ct = default);

    /// <summary>Saves changes to an existing <see cref="ClinicalDocument"/>.</summary>
    Task UpdateAsync(ClinicalDocument document, CancellationToken ct = default);

    /// <summary>
    /// Updates only the <c>extraction_status</c> column of the document identified by
    /// <paramref name="documentId"/> without fetching and re-saving the full entity.
    /// </summary>
    Task UpdateExtractionStatusAsync(Guid documentId, ExtractionStatus status, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted documents whose keys match the full-text or trigram search
    /// against <paramref name="searchTerm"/> in the <c>extracted_text</c> column.
    /// Returns <c>null</c> when the document has no extracted text or no match.
    /// </summary>
    Task<string?> SearchExtractedTextAsync(Guid documentId, string searchTerm, CancellationToken ct = default);

    // ── US_048 additions ──────────────────────────────────────────────────────

    /// <summary>
    /// Projects non-deleted documents for <paramref name="patientId"/> to
    /// <see cref="TimelineEventDto"/> list with optional date range filter applied at query time
    /// (US_048 AC-3, NFR-002).
    ///
    /// Date bounds are inclusive. Returns an empty list when no documents match (Edge Case 1).
    /// </summary>
    Task<List<TimelineEventDto>> GetTimelineDocumentsAsync(
        Guid patientId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}

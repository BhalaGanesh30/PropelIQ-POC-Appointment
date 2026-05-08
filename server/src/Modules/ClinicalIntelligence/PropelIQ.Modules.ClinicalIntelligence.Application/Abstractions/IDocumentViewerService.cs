using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Serves document content and full-text search for the in-browser document viewer (US_042).
/// </summary>
public interface IDocumentViewerService
{
    /// <summary>
    /// Returns a short-lived pre-signed Cloudflare R2 URL so the browser can load
    /// the document directly from object storage within the 3-second render target (AC-1).
    /// Returns <c>null</c> when the document does not exist or has not passed malware scan.
    /// </summary>
    Task<DocumentContentResponse?> GetDocumentContentAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// Searches the OCR-extracted text of a document for the given <paramref name="searchTerm"/>.
    /// Returns an empty response (with <c>ExtractionStatus</c> set) when OCR is still in progress
    /// so the frontend can disable search (Edge Case 1).
    /// </summary>
    Task<DocumentSearchResponse?> SearchDocumentAsync(Guid documentId, string searchTerm, CancellationToken ct = default);
}

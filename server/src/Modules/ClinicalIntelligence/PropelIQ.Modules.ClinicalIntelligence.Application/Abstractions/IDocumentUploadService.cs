using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Orchestrates the document upload workflow: type validation, size check,
/// malware scanning, R2 storage, and DB persistence (US_040 AC-1 → AC-4).
/// </summary>
public interface IDocumentUploadService
{
    /// <summary>
    /// Validates, scans, stores, and persists a clinical document upload.
    /// Throws <see cref="InvalidOperationException"/> on type/size violations (AC-1, AC-4, Edge Case 2).
    /// Throws <see cref="SecurityException"/> on confirmed malware (AC-3).
    /// Returns with <c>ScanResult = "PendingScan"</c> when scanner is unavailable (Edge Case 1).
    /// </summary>
    Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadCommand command, CancellationToken ct = default);

    /// <summary>Returns the current scan and extraction status for a document.</summary>
    Task<DocumentStatusResponse?> GetDocumentStatusAsync(Guid documentId, CancellationToken ct = default);
}

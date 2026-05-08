using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

public sealed class ClinicalDocument : BaseEntity
{
    public required Guid PatientId { get; set; }

    /// <summary>Original filename as provided by the uploader (US_040).</summary>
    public required string FileName { get; set; }

    /// <summary>
    /// User-facing display name for the document (US_043 AC-2).
    /// When null the UI falls back to <see cref="FileName"/>.
    /// The original storage filename (<see cref="FileName"/>) is never overwritten.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>MIME content-type of the uploaded file (e.g. "application/pdf").</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Size in bytes of the uploaded file.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Typed document category (US_043 AC-1).
    /// Null when the document has not yet been categorized.
    /// Independent of <see cref="ExtractionStatus"/> — categorization is allowed
    /// even while OCR is still processing (Edge Case 1).
    /// </summary>
    public DocumentCategoryType? Category { get; set; }

    /// <summary>
    /// Soft-delete flag (US_043 AC-3, AC-4).
    /// When true the document is hidden from active listings but preserved in the database.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp when the document was soft-deleted (US_043 AC-4 — trash view deletion date).
    /// Null for active documents.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Cloudflare R2 object key where the file is stored.
    /// Replaces the legacy <see cref="StoragePath"/> field for new uploads.
    /// </summary>
    public string? R2ObjectKey { get; set; }

    /// <summary>Legacy storage path — retained for backwards compatibility.</summary>
    public string? StoragePath { get; set; }

    /// <summary>Malware scan verdict (US_040 AC-2, AC-3, Edge Case 1).</summary>
    public string ScanResult { get; set; } = "PendingScan";

    /// <summary>AI/OCR extraction pipeline state (TR-005).</summary>
    public string ExtractionStatus { get; set; } = "Queued";

    /// <summary>Text extracted by the OCR/AI pipeline once extraction completes.</summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// Set to <c>true</c> when Tesseract OCR average confidence falls below the configured
    /// threshold, indicating low-quality extraction that requires clinician review (Edge Case 1).
    /// </summary>
    public bool NeedsManualReview { get; set; }

    public ICollection<ClinicalFact> ClinicalFacts { get; set; } = [];
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Enums;

/// <summary>
/// Tracks the AI/OCR extraction pipeline state for a clinical document.
/// Persisted to <c>clinical_documents.extraction_status</c>.
/// </summary>
public enum ExtractionStatus
{
    /// <summary>Queued for extraction; not yet picked up by a worker (TR-005).</summary>
    Queued,

    /// <summary>An OCR/extraction worker is actively processing the document.</summary>
    Processing,

    /// <summary>Extraction completed successfully; <c>ExtractedText</c> is populated.</summary>
    Completed,

    /// <summary>Extraction failed after all retries; see dead-letter handling (TR-005).</summary>
    Failed,
}

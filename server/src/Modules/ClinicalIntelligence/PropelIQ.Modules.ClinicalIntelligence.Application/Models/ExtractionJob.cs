namespace PropelIQ.Modules.ClinicalIntelligence.Application.Models;

/// <summary>
/// Message dispatched to <c>ExtractionJobChannel</c> when a document's OCR text is
/// ready for clinical entity extraction.
/// </summary>
/// <param name="DocumentId">Primary key of the <c>clinical_documents</c> record.</param>
/// <param name="PatientId">Patient the document belongs to (used for PII context).</param>
/// <param name="ExtractedText">OCR-produced text from the document.</param>
/// <param name="RetryCount">Number of prior failed attempts (incremented on re-enqueue).</param>
public sealed record ExtractionJob(
    Guid DocumentId,
    Guid PatientId,
    string ExtractedText,
    int RetryCount = 0);

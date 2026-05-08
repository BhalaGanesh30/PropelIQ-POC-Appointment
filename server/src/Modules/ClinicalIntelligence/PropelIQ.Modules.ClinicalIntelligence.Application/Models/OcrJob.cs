namespace PropelIQ.Modules.ClinicalIntelligence.Application.Models;

/// <summary>
/// Message dispatched to <c>OcrJobChannel</c> when a document is ready for OCR processing.
/// Carries the minimum information needed by the worker to download and process the file.
/// </summary>
/// <param name="DocumentId">Primary key of the <c>clinical_documents</c> record.</param>
/// <param name="R2ObjectKey">Cloudflare R2 object key used to download the file.</param>
/// <param name="RetryCount">Number of prior failed attempts (incremented on re-enqueue).</param>
public sealed record OcrJob(
    Guid DocumentId,
    string R2ObjectKey,
    int RetryCount = 0);

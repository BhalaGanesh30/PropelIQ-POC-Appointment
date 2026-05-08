using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Processes a single OCR job: downloads the file from R2, executes Tesseract OCR,
/// and returns the extracted text with confidence metrics.
/// </summary>
public interface IOcrProcessingService
{
    /// <summary>
    /// Downloads the document referenced by <paramref name="job"/> from Cloudflare R2,
    /// runs Tesseract OCR, and returns the extraction result.
    /// </summary>
    /// <param name="job">The OCR job containing the R2 object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="OcrProcessingResult"/> with extracted text, average confidence,
    /// and a manual-review flag for low-confidence results (Edge Case 1).
    /// </returns>
    Task<OcrProcessingResult> ProcessDocumentAsync(OcrJob job, CancellationToken ct = default);
}

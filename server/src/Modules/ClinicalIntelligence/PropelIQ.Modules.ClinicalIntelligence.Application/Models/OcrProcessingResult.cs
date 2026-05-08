namespace PropelIQ.Modules.ClinicalIntelligence.Application.Models;

/// <summary>
/// Result returned by <c>IOcrProcessingService.ProcessDocumentAsync</c>.
/// </summary>
/// <param name="ExtractedText">Full text extracted by Tesseract OCR.</param>
/// <param name="AverageConfidence">
/// Mean character confidence score (0.0–1.0) across all recognized blocks.
/// </param>
/// <param name="NeedsManualReview">
/// <c>true</c> when <see cref="AverageConfidence"/> is below the configured threshold,
/// indicating low-quality OCR output that should be reviewed by a clinician (Edge Case 1).
/// </param>
public sealed record OcrProcessingResult(
    string ExtractedText,
    double AverageConfidence,
    bool NeedsManualReview);

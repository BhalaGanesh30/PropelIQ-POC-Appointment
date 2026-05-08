namespace PropelIQ.Modules.ClinicalIntelligence.Application.Models;

/// <summary>
/// Aggregated result returned by <c>IClinicalExtractionService</c> after processing
/// a single document.  Consumed by <c>ExtractionWorkerService</c> to update the
/// document status and surface metrics.
/// </summary>
/// <param name="Facts">Extracted and normalized clinical facts.</param>
/// <param name="LowInputQuality">
/// True when OCR text was too short or low-quality for meaningful extraction.
/// The pipeline returns empty <see cref="Facts"/> and flags the document for manual review (Edge Case 1).
/// </param>
/// <param name="SchemaValidationPassCount">
/// Number of AI response chunks that passed JSON schema validation (AC-4, AIR-008).
/// </param>
/// <param name="SchemaValidationTotalCount">
/// Total number of AI response chunks submitted to schema validation.
/// </param>
public sealed record ExtractionResult(
    IReadOnlyList<ExtractedFact> Facts,
    bool LowInputQuality,
    int SchemaValidationPassCount,
    int SchemaValidationTotalCount);

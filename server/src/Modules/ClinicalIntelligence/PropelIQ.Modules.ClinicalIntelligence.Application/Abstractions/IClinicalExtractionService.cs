using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Orchestrates the clinical entity extraction pipeline for a single document.
/// Implementations chunk the OCR text, redact PII, call the AI gateway,
/// validate and normalize the response, and persist extracted facts.
/// </summary>
public interface IClinicalExtractionService
{
    /// <summary>
    /// Runs the full extraction pipeline for the given job and returns aggregated results.
    /// </summary>
    Task<ExtractionResult> ExtractEntitiesAsync(ExtractionJob job, CancellationToken ct = default);
}

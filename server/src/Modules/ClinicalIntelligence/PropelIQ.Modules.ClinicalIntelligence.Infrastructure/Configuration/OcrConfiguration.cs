namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the asynchronous OCR processing pipeline.
/// Bound from <c>appsettings.json</c> section <c>"Ocr"</c> (TR-005).
/// </summary>
public sealed class OcrConfiguration
{
    public const string SectionName = "Ocr";

    /// <summary>Maximum number of retries before a job is moved to the dead-letter queue (AC-4).</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Number of concurrent OCR worker tasks (Edge Case 2).</summary>
    public int ConcurrencyLimit { get; set; } = 4;

    /// <summary>
    /// Average character confidence threshold (0.0–1.0) below which a document
    /// is flagged for manual review (Edge Case 1). Default: 0.60.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.60;

    /// <summary>Filesystem path to the Tesseract tessdata directory.</summary>
    public string TessdataPath { get; set; } = "./tessdata";

    /// <summary>Base delay in seconds for exponential backoff (1s, 4s, 16s).</summary>
    public int BackoffBaseSeconds { get; set; } = 1;
}

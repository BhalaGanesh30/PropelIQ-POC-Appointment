namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the clinical entity extraction pipeline.
/// Bound from <c>appsettings.json</c> section <c>"Extraction"</c>.
/// </summary>
public sealed class ExtractionConfiguration
{
    public const string SectionName = "Extraction";

    /// <summary>
    /// Confidence score (0.0 – 1.0) below which a fact is flagged
    /// <c>NeedsReview = true</c> (AC-3, AIR-005). Default: 0.70.
    /// </summary>
    public decimal ConfidenceThreshold { get; set; } = 0.70m;

    /// <summary>
    /// Maximum number of characters per text chunk sent to the AI gateway.
    /// Keeps prompts within the model token budget (AIR-001). Default: 4000.
    /// </summary>
    public int MaxChunkSize { get; set; } = 4000;

    /// <summary>Number of concurrent extraction consumer tasks. Default: 2.</summary>
    public int ConcurrencyLimit { get; set; } = 2;

    /// <summary>Maximum retry attempts before moving a job to the dead-letter queue.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Minimum extracted-text length in characters.
    /// Text shorter than this is considered low-quality and triggers the
    /// <c>LowInputQuality</c> path (Edge Case 1).
    /// </summary>
    public int LowQualityTextLengthThreshold { get; set; } = 50;

    /// <summary>Base delay in seconds for exponential backoff between retries.</summary>
    public int BackoffBaseSeconds { get; set; } = 1;

    /// <summary>
    /// AI gateway model alias to use for extraction prompts.
    /// Defaults to <c>"gpt-4.1"</c> as defined in the LiteLLM proxy config.
    /// </summary>
    public string ExtractionModelId { get; set; } = "gpt-4.1";

    /// <summary>Max tokens allowed in the AI completion response.</summary>
    public int MaxTokens { get; set; } = 4096;
}

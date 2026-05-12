namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Options;

/// <summary>
/// Configuration options for the ICD-10 coding suggestion pipeline.
/// Bound from <c>appsettings.json</c> section <c>"AI"</c>.
/// </summary>
public sealed class CodingSuggestionOptions
{
    /// <summary>
    /// Minimum confidence score to consider a suggestion "high confidence" (US_049 AC-4).
    /// Suggestions below this threshold set <c>LowConfidence = true</c> in the response,
    /// triggering the amber banner in the frontend.
    /// Default: 0.75.
    /// </summary>
    public decimal ConfidenceThreshold { get; init; } = 0.75m;
}

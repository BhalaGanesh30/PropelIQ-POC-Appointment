using System.Text.Json.Serialization;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Internal deserialization model for GPT-4.1 CPT/E/M structured output (AIR-008).
/// Maps: <c>{ cpt_suggestions: [...], em_suggestion: {...} }</c>
/// </summary>
internal sealed class LlmCptResponse
{
    [JsonPropertyName("cpt_suggestions")]
    public List<LlmCptSuggestionItem> CptSuggestions { get; set; } = [];

    [JsonPropertyName("em_suggestion")]
    public LlmEmSuggestionItem? EmSuggestion { get; set; }
}

internal sealed class LlmCptSuggestionItem
{
    [JsonPropertyName("cpt_code")]
    public string? CptCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }

    [JsonPropertyName("fact_ids")]
    public List<Guid> FactIds { get; set; } = [];
}

internal sealed class LlmEmSuggestionItem
{
    [JsonPropertyName("em_level")]
    public string? EmLevel { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }

    [JsonPropertyName("complexity_factors")]
    public List<string> ComplexityFactors { get; set; } = [];
}

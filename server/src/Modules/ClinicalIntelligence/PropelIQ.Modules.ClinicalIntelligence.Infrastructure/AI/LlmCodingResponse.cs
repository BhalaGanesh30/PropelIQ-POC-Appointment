using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Internal LLM response model deserialized from the GPT-4.1 structured output (AIR-008).
/// Maps the JSON schema: <c>{ suggestions: [ { icd10_code, description, confidence, rationale, fact_ids[] } ] }</c>
/// </summary>
internal sealed class LlmCodingResponse
{
    public List<LlmSuggestionItem> Suggestions { get; set; } = [];
}

internal sealed class LlmSuggestionItem
{
    public string? Icd10Code { get; set; }
    public string? Description { get; set; }
    public decimal Confidence { get; set; }
    public string? Rationale { get; set; }
    public List<Guid> FactIds { get; set; } = [];
}

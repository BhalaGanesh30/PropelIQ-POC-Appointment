namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Per-suggestion CPT procedure code DTO (US_050 AC-2, AIR-004).
/// </summary>
public sealed record CptSuggestionDto
{
    public required Guid DecisionId { get; init; }
    public required string CptCode { get; init; }
    public required string Description { get; init; }
    public required decimal Confidence { get; init; }
    public required string Rationale { get; init; }
    public required List<ClinicalFactCitationDto> Citations { get; init; }
}

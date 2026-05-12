namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// E/M (Evaluation and Management) level suggestion DTO (US_050 AC-3, AIR-003).
/// </summary>
public sealed record EmSuggestionDto
{
    public required Guid DecisionId { get; init; }

    /// <summary>E/M level code, e.g. "99213".</summary>
    public required string EmLevel { get; init; }
    public required string Description { get; init; }
    public required decimal Confidence { get; init; }
    public required string Rationale { get; init; }

    /// <summary>
    /// Clinical complexity factors that contributed to this E/M level.
    /// Rendered as a collapsible list on the FE card (AC-3, UXR-204).
    /// </summary>
    public required List<string> ComplexityFactors { get; init; }
}

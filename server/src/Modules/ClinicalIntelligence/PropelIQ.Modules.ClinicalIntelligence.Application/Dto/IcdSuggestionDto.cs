namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// A single AI-generated ICD-10 code suggestion (FR-MC-001, AIR-003).
/// </summary>
public sealed record IcdSuggestionDto
{
    /// <summary>GUID of the persisted <c>coding_decisions</c> row for audit trail.</summary>
    public required Guid DecisionId { get; init; }

    /// <summary>ICD-10 code (e.g. "J18.9").</summary>
    public required string IcdCode { get; init; }

    /// <summary>Human-readable ICD-10 description.</summary>
    public required string Description { get; init; }

    /// <summary>Model confidence score in range 0.0–1.0 (AIR-005).</summary>
    public required decimal Confidence { get; init; }

    /// <summary>Explainable rationale linked to clinical evidence (AIR-003).</summary>
    public required string Rationale { get; init; }

    /// <summary>Clinical fact citations supporting this suggestion (AIR-004, AC-2).</summary>
    public required IReadOnlyList<ClinicalFactCitationDto> Citations { get; init; }
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Response envelope for <c>GET /api/v1/patients/{id}/coding-suggestions</c> (US_049).
/// </summary>
public sealed record CodingSuggestionResponseDto
{
    /// <summary>Up to 3 ranked ICD-10 suggestions sorted by confidence descending (AC-1).</summary>
    public required IReadOnlyList<IcdSuggestionDto> Suggestions { get; init; }

    /// <summary>
    /// True when the lowest suggestion confidence falls below the configured threshold.
    /// Triggers the "Manual review recommended" banner in the UI (AC-3, AIR-005).
    /// </summary>
    public required bool LowConfidence { get; init; }

    /// <summary>
    /// True when the AI pipeline could not produce the full 3 suggestions due to
    /// limited clinical data (Edge Case 1). Never padded with placeholder codes.
    /// </summary>
    public required bool InsufficientEvidence { get; init; }
}

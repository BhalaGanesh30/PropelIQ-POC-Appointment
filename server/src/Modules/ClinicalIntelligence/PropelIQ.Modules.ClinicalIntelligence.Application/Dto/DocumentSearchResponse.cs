namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Returned by <c>GET /api/v1/documents/{id}/search?term=...</c>.
/// Carries matched text snippets with position context and the current extraction
/// status so the frontend can disable search while OCR is still running (Edge Case 1).
/// </summary>
public sealed class DocumentSearchResponse
{
    /// <summary>Individual text match occurrences with surrounding context.</summary>
    public List<SearchMatchDto> Matches { get; set; } = [];

    /// <summary>Total number of matches found (may exceed <see cref="Matches"/> if capped).</summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current OCR extraction state. Frontend uses this to show a
    /// "search unavailable — OCR in progress" message when not <c>Completed</c>.
    /// </summary>
    public string ExtractionStatus { get; set; } = string.Empty;
}

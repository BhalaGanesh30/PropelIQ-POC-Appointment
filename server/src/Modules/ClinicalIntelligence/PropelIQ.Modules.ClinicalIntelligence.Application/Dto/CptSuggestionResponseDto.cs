namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Response wrapper for the CPT / E/M suggestion endpoint (US_050).
///
/// Always returned as HTTP 200 — edge cases are communicated via flags:
/// - <see cref="NoSuggestionForAppointmentType"/> — appointment type is not mappable to CPT (Edge Case 1).
/// - <see cref="StaleDatabaseWarning"/>           — CPT catalog is older than 90 days (Edge Case 2).
/// - <see cref="LowConfidence"/>                  — min CPT confidence is below threshold (AC-4, AIR-005).
/// </summary>
public sealed record CptSuggestionResponseDto
{
    public required List<CptSuggestionDto> CptSuggestions { get; init; }
    public EmSuggestionDto? EmSuggestion { get; init; }

    /// <summary>
    /// <c>true</c> when the top CPT suggestion confidence is below the configured threshold (AIR-005).
    /// </summary>
    public bool LowConfidence { get; init; }

    /// <summary>
    /// <c>true</c> when the CPT code catalog was last updated more than 90 days ago (Edge Case 2).
    /// Suggestions are still returned; the FE renders an amber warning banner.
    /// </summary>
    public bool StaleDatabaseWarning { get; init; }

    /// <summary>
    /// <c>true</c> when the appointment type has no CPT mapping (Edge Case 1).
    /// CptSuggestions will be empty and EmSuggestion will be null.
    /// </summary>
    public bool NoSuggestionForAppointmentType { get; init; }
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Extended fact DTO returned by write endpoints (PATCH and POST /verify).
///
/// Carries the new <see cref="ETag"/> (row_version after update) so the client
/// can issue future PATCH requests with a fresh If-Match header.
/// <see cref="ReferencedByCodingDecision"/> surfaces the amber warning when
/// the edited fact is linked to a coding decision (US_047 Edge Case 2).
/// </summary>
public sealed record ClinicalFactResponseDto : ClinicalFactDto
{
    /// <summary>String representation of the new <c>row_version</c> after the write (US_047 Edge Case 1).</summary>
    public string ETag { get; init; } = string.Empty;

    /// <summary>True when at least one <c>coding_decision</c> row references this fact (US_047 Edge Case 2).</summary>
    public bool ReferencedByCodingDecision { get; init; }
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Response envelope for the code search endpoint (US_052, AC-1).
///
/// Edge Case 1: When no codes match the query, <see cref="Results"/> is empty
/// and <see cref="TotalCount"/> is 0 — HTTP 200 is always returned.
/// </summary>
public sealed class CodeSearchResponseDto
{
    /// <summary>Ordered list of matching codes; favorites pinned first (AC-3).</summary>
    public required IReadOnlyList<CodeResultDto> Results { get; init; }

    /// <summary>Total number of results returned (post-filter count).</summary>
    public required int TotalCount { get; init; }
}

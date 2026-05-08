namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Response envelope for GET /api/v1/patients/{id}/timeline (US_048).
///
/// Always HTTP 200 — <see cref="Events"/> is an empty list when no events exist (Edge Case 1).
/// <see cref="TotalCount"/> equals <c>Events.Count</c> (no server-side paging — caller applies
/// client-side virtual scroll / year grouping with this count).
/// </summary>
public sealed record TimelineResponseDto
{
    /// <summary>
    /// Timeline events in reverse-chronological order (most-recent first, AC-1).
    /// Empty when the patient has no clinical events (Edge Case 1).
    /// </summary>
    public required IReadOnlyList<TimelineEventDto> Events { get; init; }

    /// <summary>
    /// Total number of events matching the applied filters.
    /// Used by the FE to inform year-grouping and virtual scroll rendering (Edge Case 2).
    /// </summary>
    public required int TotalCount { get; init; }
}

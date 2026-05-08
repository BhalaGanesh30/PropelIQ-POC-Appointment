namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Response envelope for the conflict detection endpoint (AC-1, AC-2, Edge Case 1).
///
/// Alerts are sorted Critical → High → Moderate → Low by the detection service.
/// </summary>
public sealed record ConflictAlertsResponseDto
{
    /// <summary>
    /// Detected conflict alerts, deduplicated to highest severity per drug pair (Edge Case 2),
    /// sorted Critical → High → Moderate → Low.
    /// </summary>
    public required IReadOnlyList<ConflictAlertDto> Alerts { get; init; }

    /// <summary>
    /// True when the newest <c>conflict_rules.last_updated_at</c> timestamp exceeds the
    /// configured staleness threshold (default 30 days). The frontend surfaces an amber
    /// "rules may be outdated" warning when this flag is set (Edge Case 1).
    /// </summary>
    public required bool RulesStale { get; init; }
}

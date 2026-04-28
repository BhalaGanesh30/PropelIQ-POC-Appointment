namespace PropelIQ.Modules.Scheduling.Application.AI.Models;

/// <summary>
/// Result returned by the no-show risk scoring service.
/// AC-1: Contains risk label, confidence score, and explainable feature contributions.
/// </summary>
public sealed record NoShowRiskResult(
    string RiskLevel,
    double Confidence,
    IReadOnlyList<RiskFeatureContribution> Features);

/// <summary>
/// A single feature contribution explaining the risk classification (AIR-004).
/// </summary>
public sealed record RiskFeatureContribution(
    string Name,
    string Contribution);

/// <summary>
/// Shared constants and fallback defaults for no-show risk scoring.
/// </summary>
public static class NoShowRiskDefaults
{
    /// <summary>
    /// Returned when the AI gateway is unavailable or returns an invalid response.
    /// Edge case 1: staff see "Unknown" with no false risk indicators shown.
    /// </summary>
    public static readonly NoShowRiskResult Unknown = new(
        "Unknown", 0.0,
        Array.Empty<RiskFeatureContribution>());

    /// <summary>
    /// A cached score older than this threshold is considered stale and
    /// recalculated on next access (edge case 2).
    /// </summary>
    public static readonly TimeSpan StalenessThreshold = TimeSpan.FromHours(24);
}

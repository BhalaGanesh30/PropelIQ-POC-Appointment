namespace PropelIQ.Modules.Scheduling.Infrastructure.Walkin;

/// <summary>
/// Configuration options for walk-in management (EP-004 US_033).
/// Bound from <c>appsettings.json</c> section <c>"WalkIn"</c>.
/// </summary>
public sealed class WalkinOptions
{
    public const string SectionName = "WalkIn";

    /// <summary>
    /// Maximum number of active appointments today before the capacity warning
    /// flag is set in the walk-in creation response (Edge Case 2).
    /// Default: 50.
    /// </summary>
    public int CapacityThreshold { get; set; } = 50;
}

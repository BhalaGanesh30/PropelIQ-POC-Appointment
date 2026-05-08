namespace PropelIQ.Modules.Scheduling.Infrastructure.Queue;

/// <summary>
/// Configuration options for the queue API (EP-004 US_031).
/// Bound from <c>appsettings.json</c> section <c>"Queue"</c>.
/// </summary>
public sealed class QueueOptions
{
    public const string SectionName = "Queue";

    /// <summary>
    /// Redis absolute expiry in seconds for the <c>queue:today:*</c> cache keys.
    /// Default: 15 seconds (NFR-002 — refreshes within AC-2's 5 s window).
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 15;
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// CPT code catalog freshness check (US_050, Edge Case 2).
/// Result is cached in Redis for 1 hour to avoid repeated DB scans.
/// </summary>
public interface ICptCodeFreshnessService
{
    /// <summary>
    /// Returns whether the CPT catalog is older than the configured threshold (default 90 days).
    /// </summary>
    Task<CptFreshnessResult> CheckFreshnessAsync(CancellationToken ct = default);
}

/// <summary>Result of the CPT catalog freshness check.</summary>
public sealed record CptFreshnessResult
{
    public required bool IsStale { get; init; }
    public required DateTimeOffset? LastUpdatedAt { get; init; }
}

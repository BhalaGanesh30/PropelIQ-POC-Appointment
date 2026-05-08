namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Query parameters for the clinical timeline endpoint (US_048, AC-2, AC-3, Edge Case 2).
///
/// All fields are optional:
///   - When <see cref="Category"/> is null or "All", events from every source are returned.
///   - Date filters are applied server-side at query time to support NFR-002 (&lt;500 ms p95)
///     on large patient timelines.
/// </summary>
public sealed record TimelineQuery
{
    /// <summary>
    /// Case-insensitive category filter: "Medications", "Allergies", "Diagnoses",
    /// "Findings", "Documents", or null / "All" for all categories.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>Inclusive start of the event date range (ISO 8601). Null means no lower bound.</summary>
    public DateTimeOffset? DateFrom { get; init; }

    /// <summary>Inclusive end of the event date range (ISO 8601). Null means no upper bound.</summary>
    public DateTimeOffset? DateTo { get; init; }
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Query parameters for the patient profile aggregation endpoint.
/// AC-2 (Edge Case 2): pagination enables virtual scroll for large profiles (100+ facts).
/// </summary>
public sealed record ProfileQuery
{
    /// <summary>Maximum facts to return per category (1–100). Defaults to 50.</summary>
    public int Limit { get; init; } = 50;

    /// <summary>Zero-based offset for pagination. Defaults to 0.</summary>
    public int Offset { get; init; } = 0;

    /// <summary>Tab identifier hint: "summary", "timeline", or a specific fact type.</summary>
    public string Tab { get; init; } = "summary";
}

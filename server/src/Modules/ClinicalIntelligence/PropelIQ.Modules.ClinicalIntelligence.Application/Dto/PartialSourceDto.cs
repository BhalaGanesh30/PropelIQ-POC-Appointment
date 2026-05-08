namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Identifies a fact category that failed to load during profile aggregation (Edge Case 1).
/// Returned in <see cref="PatientProfileDto.PartialSources"/> when <see cref="PatientProfileDto.Partial"/> is true.
/// </summary>
public sealed record PartialSourceDto
{
    /// <summary>Name of the category that failed (e.g. "medication", "allergy").</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Sanitized human-readable reason for the failure.
    /// Must NOT contain internal stack traces or system internals (security requirement).
    /// </summary>
    public string ErrorReason { get; init; } = string.Empty;
}

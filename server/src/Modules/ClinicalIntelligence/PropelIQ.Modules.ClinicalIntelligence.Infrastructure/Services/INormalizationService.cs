using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Applies deterministic post-processing rules to AI-extracted facts to standardize
/// naming conventions for medications, allergies, and diagnoses (AIR-001 hybrid pattern).
/// </summary>
public interface INormalizationService
{
    /// <summary>
    /// Returns a new list of facts with normalized <see cref="ExtractedFact.Name"/> and
    /// <see cref="ExtractedFact.Value"/> fields.  Facts whose type is unknown or whose
    /// values cannot be normalized are passed through unchanged.
    /// </summary>
    IReadOnlyList<ExtractedFact> Normalize(IReadOnlyList<ExtractedFact> facts);
}

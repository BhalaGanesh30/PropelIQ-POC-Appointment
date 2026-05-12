namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Deterministic post-LLM CPT code validation guardrail (US_050, FR-MC-002 Hybrid pattern).
///
/// Removes any LLM-hallucinated or deprecated codes from the result set before the
/// response is built.  This is the deterministic layer of the Hybrid pipeline.
/// </summary>
public interface ICptCodeValidationService
{
    /// <summary>
    /// Filters <paramref name="cptCodes"/> to the subset that exists in the
    /// <c>cpt_codes</c> catalog with <c>is_deprecated = false</c>.
    /// Returns the valid subset as a set (may be empty).
    /// </summary>
    Task<IReadOnlySet<string>> FilterActiveAsync(
        IEnumerable<string> cptCodes,
        CancellationToken ct = default);
}

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for querying coding decisions to detect fact references (US_047 Edge Case 2).
/// </summary>
public interface ICodingDecisionRepository
{
    /// <summary>
    /// Returns <c>true</c> when at least one <c>coding_decisions</c> row references
    /// the given fact (US_047 Edge Case 2 — edit allowed but FE should show amber warning).
    /// </summary>
    /// <param name="factId">Primary key of the clinical fact.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ExistsForFactAsync(Guid factId, CancellationToken ct = default);
}

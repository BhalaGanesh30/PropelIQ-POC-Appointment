using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// Repository abstraction for <see cref="ConflictRule"/> read access.
/// Rules are read-only at runtime — they are seeded during deployment.
/// </summary>
public interface IConflictRuleRepository
{
    /// <summary>
    /// Returns all active conflict rules (<c>is_active = true</c>).
    /// Results are memory-cached for 5 minutes since rules change infrequently.
    /// </summary>
    Task<IReadOnlyList<ConflictRule>> GetActiveRulesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns <c>MAX(last_updated_at)</c> across all rules, or null if the table is empty.
    /// Used by Edge Case 1 staleness check.
    /// </summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken ct = default);
}

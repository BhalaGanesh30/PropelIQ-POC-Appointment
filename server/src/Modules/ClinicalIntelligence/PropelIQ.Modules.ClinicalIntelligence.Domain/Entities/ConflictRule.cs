using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

/// <summary>
/// A clinical interaction rule used by the conflict detection engine.
///
/// Each row describes one known drug-drug or drug-allergy interaction with its
/// severity classification and a human-readable description.
///
/// Maps to the <c>app.conflict_rules</c> table (task_003 migration).
/// </summary>
public sealed class ConflictRule : BaseEntity
{
    /// <summary>"drug_drug" or "drug_allergy" — determines which cross-product to evaluate.</summary>
    public required string RuleType { get; set; }

    /// <summary>
    /// Normalized canonical name for drug A.
    /// Matched case-insensitively against <c>ClinicalFact.Name</c>.
    /// </summary>
    public required string DrugAName { get; set; }

    /// <summary>
    /// Normalized canonical name for drug B (second drug) or allergen.
    /// For drug-drug rules the pair is order-insensitive.
    /// </summary>
    public required string DrugBName { get; set; }

    /// <summary>"low", "moderate", "high", or "critical".</summary>
    public required string Severity { get; set; }

    /// <summary>Human-readable description of the interaction risk.</summary>
    public required string Description { get; set; }

    /// <summary>Origin of this rule (e.g. "system", "fda", "admin-import").</summary>
    public string Source { get; set; } = "system";

    /// <summary>False for retired/superseded rules that should not be evaluated.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time this rule was reviewed or updated.
    /// Used by Edge Case 1 staleness check — if the newest row exceeds the staleness
    /// threshold, the response includes <c>rulesStale: true</c>.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

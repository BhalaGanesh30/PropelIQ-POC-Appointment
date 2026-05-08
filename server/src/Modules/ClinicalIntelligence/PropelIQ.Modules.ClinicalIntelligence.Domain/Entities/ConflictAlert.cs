using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

/// <summary>
/// Persisted conflict detection result for a specific patient and drug pair.
///
/// Rows are upserted by <c>ConflictDetectionService</c> each time the detection
/// endpoint is called — idempotent on <c>(PatientId, FactIdA, FactIdB)</c>.
///
/// Maps to the <c>app.conflict_alerts</c> table (task_003 migration).
/// </summary>
public sealed class ConflictAlert : BaseEntity
{
    /// <summary>Patient the conflict was detected for.</summary>
    public required Guid PatientId { get; set; }

    /// <summary>
    /// FK to the first ClinicalFact (medication).
    /// Used for deduplication key together with <see cref="FactIdB"/>.
    /// </summary>
    public required Guid FactIdA { get; set; }

    /// <summary>
    /// FK to the second ClinicalFact (medication for drug-drug; allergy for drug-allergy).
    /// Nullable: some drug-allergy alerts have no matching second clinical fact row.
    /// </summary>
    public Guid? FactIdB { get; set; }

    /// <summary>FK to the <see cref="ConflictRule"/> that triggered this alert.</summary>
    public required Guid RuleId { get; set; }

    /// <summary>"drug_drug" or "drug_allergy" matching the rule_type in conflict_rules.</summary>
    public required string ConflictType { get; set; }

    /// <summary>"low", "moderate", "high", or "critical".</summary>
    public required string Severity { get; set; }

    /// <summary>Human-readable conflict description from the matched rule.</summary>
    public required string Description { get; set; }

    /// <summary>Canonical name of drug A used to match the rule.</summary>
    public required string DrugA { get; set; }

    /// <summary>Canonical name of drug B (or allergen) used to match the rule.</summary>
    public required string DrugB { get; set; }

    /// <summary>True once a clinician has acknowledged this alert (AC-3).</summary>
    public bool Acknowledged { get; set; }

    /// <summary>FK of the clinician who acknowledged. Null until acknowledged.</summary>
    public Guid? AcknowledgedBy { get; set; }

    /// <summary>UTC timestamp when acknowledged. Null until acknowledged.</summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }
}

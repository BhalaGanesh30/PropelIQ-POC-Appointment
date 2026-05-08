namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Response DTO for a single detected conflict alert (AC-2).
///
/// Maps 1:1 to the <c>conflict_alerts</c> table row augmented with evaluated
/// severity metadata from the matched <c>conflict_rules</c> rule.
/// </summary>
public sealed record ConflictAlertDto
{
    /// <summary>Conflict alert row identifier.</summary>
    public required Guid ConflictId { get; init; }

    /// <summary>"drug_drug" or "drug_allergy" — matches the rule_type in conflict_rules.</summary>
    public required string ConflictType { get; init; }

    /// <summary>"low", "moderate", "high", or "critical" — from the matched conflict rule.</summary>
    public required string Severity { get; init; }

    /// <summary>Human-readable description of the conflict from the matched rule.</summary>
    public required string Description { get; init; }

    /// <summary>Canonical name of the first drug (or medication) in the conflict pair.</summary>
    public required string DrugA { get; init; }

    /// <summary>Canonical name of the second drug or allergen in the pair.</summary>
    public required string DrugB { get; init; }

    /// <summary>True once a clinician has acknowledged this conflict (AC-3).</summary>
    public required bool Acknowledged { get; init; }

    /// <summary>UTC timestamp when the conflict was acknowledged. Null if unacknowledged.</summary>
    public DateTimeOffset? AcknowledgedAt { get; init; }

    /// <summary>Display name of the clinician who acknowledged. Null if unacknowledged.</summary>
    public string? AcknowledgedBy { get; init; }
}

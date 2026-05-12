namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Represents a single coding decision that still awaits clinician review
/// (reviewer_action = 'Pending'). Returned by GET /api/v1/patients/{id}/coding-decisions/pending
/// to populate the AC-4 submission block banner in the FE (US_051).
/// </summary>
public sealed record PendingDecisionDto
{
    /// <summary>Primary key of the <c>coding_decisions</c> row.</summary>
    public required Guid DecisionId { get; init; }

    /// <summary>
    /// AI-suggested ICD-10 code. Null for CPT-only decisions.
    /// </summary>
    public string? IcdCode { get; init; }

    /// <summary>
    /// AI-suggested CPT code. Null for ICD-only decisions.
    /// </summary>
    public string? CptCode { get; init; }

    /// <summary>Patient this decision belongs to.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>Timestamp when the pending decision row was created (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

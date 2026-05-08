using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

public sealed class CodingDecision : BaseEntity
{
    public required Guid PatientId { get; set; }
    public required Guid DocumentId { get; set; }
    public required string CodeType { get; set; }
    public required string SuggestedCode { get; set; }
    public string? Rationale { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string ReviewerAction { get; set; } = "Pending";
    public string? FinalizedCode { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>FK to the clinical fact this decision was derived from. Null for document-level decisions (US_047 Edge Case 2).</summary>
    public Guid? FactId { get; set; }

    public ClinicalDocument Document { get; set; } = null!;
}

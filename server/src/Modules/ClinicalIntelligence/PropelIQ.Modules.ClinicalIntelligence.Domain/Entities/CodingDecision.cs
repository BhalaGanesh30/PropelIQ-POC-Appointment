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

    public ClinicalDocument Document { get; set; } = null!;
}

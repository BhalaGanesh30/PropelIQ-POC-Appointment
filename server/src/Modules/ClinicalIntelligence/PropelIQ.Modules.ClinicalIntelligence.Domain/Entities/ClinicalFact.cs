using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

public sealed class ClinicalFact : BaseEntity
{
    public required Guid DocumentId { get; set; }
    public required string FactType { get; set; }
    public required string Value { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string VerificationState { get; set; } = "Unverified";
    public Guid? LastReviewedByUserId { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }

    public ClinicalDocument Document { get; set; } = null!;
}

using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

public sealed class ClinicalDocument : BaseEntity
{
    public required Guid PatientId { get; set; }
    public required string FileName { get; set; }
    public required string Category { get; set; }
    public string ExtractionStatus { get; set; } = "Pending";
    public string? StoragePath { get; set; }

    public ICollection<ClinicalFact> ClinicalFacts { get; set; } = [];
}

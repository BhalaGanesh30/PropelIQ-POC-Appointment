using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Administration.Domain.Entities;

public sealed class InsuranceProfile : BaseEntity
{
    public required Guid PatientId { get; set; }
    public required string PayerName { get; set; }
    public required string MemberId { get; set; }
    public bool IsPrimary { get; set; }
    public string VerificationStatus { get; set; } = "Pending";

    public Patient Patient { get; set; } = null!;
}

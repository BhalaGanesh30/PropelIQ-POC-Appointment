using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Administration.Domain.Entities;

public sealed class Patient : BaseEntity
{
    public required Guid UserId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required DateOnly DateOfBirth { get; set; }
    public required string MRN { get; set; }
    public ContactPreferences ContactPreferences { get; set; } = new();

    public User User { get; set; } = null!;
    public ICollection<InsuranceProfile> InsuranceProfiles { get; set; } = [];
}

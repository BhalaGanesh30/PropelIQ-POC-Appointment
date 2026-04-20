using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Administration.Domain.Entities;

public sealed class User : BaseEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }

    public Patient? PatientProfile { get; set; }
}

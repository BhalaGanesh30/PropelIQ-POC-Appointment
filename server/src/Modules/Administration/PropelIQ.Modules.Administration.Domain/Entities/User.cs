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

    /// <summary>
    /// Structural user category used for role-assignment validation (US_061, edge case 2).
    /// Allowed values: "Patient", "Staff", "Clinician", "Admin".
    /// Defaults to "Staff" for existing rows via the migration column default.
    /// </summary>
    public string UserType { get; set; } = "Staff";

    public Patient? PatientProfile { get; set; }
}

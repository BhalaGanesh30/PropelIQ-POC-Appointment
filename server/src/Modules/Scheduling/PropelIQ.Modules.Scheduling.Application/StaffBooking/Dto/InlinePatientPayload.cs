using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;

/// <summary>
/// Nested DTO for creating a new patient profile inline during staff-assisted booking (AC-3).
/// The created account is non-activated (no password) pending a separate activation flow.
/// </summary>
public sealed class InlinePatientPayload
{
    [Required]
    [MaxLength(100)]
    public required string FirstName { get; init; }

    [Required]
    [MaxLength(100)]
    public required string LastName { get; init; }

    /// <summary>Phone number — validated format server-side.</summary>
    [Required]
    [MaxLength(30)]
    public required string Phone { get; init; }

    /// <summary>ISO date of birth (YYYY-MM-DD).</summary>
    [Required]
    public required DateOnly DateOfBirth { get; init; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

/// <summary>
/// Request body for POST /api/v1/walkins/{id}/convert (EP-004 US_033 AC-2).
/// Provides the demographics needed to create a full patient account from an
/// anonymous walk-in record.
/// </summary>
public sealed class ConvertWalkinRequest
{
    /// <summary>Patient date of birth (required for patient record creation).</summary>
    [Required]
    public required DateOnly DateOfBirth { get; init; }

    /// <summary>Patient email address for account creation (required).</summary>
    [Required]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public required string Email { get; init; }

    /// <summary>Optional contact phone override; falls back to the phone stored on the walk-in.</summary>
    [RegularExpression(@"^\+?[\d\s\-()+]{7,20}$",
        ErrorMessage = "Phone must be 7–20 characters containing digits, spaces, dashes, or parentheses.")]
    public string? Phone { get; init; }
}

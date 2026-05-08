using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

/// <summary>
/// Request body for POST /api/v1/walkins (EP-004 US_033 AC-1, AC-2, AC-4).
///
/// AC-1: PatientName + VisitReason are the minimum required fields.
/// AC-2: Set ConvertToPatient=true and provide DateOfBirth + Email to create
///       a full patient account inline.
/// AC-4: Provide ExistingPatientId to link to an existing patient account without
///       duplicating the record (disambiguation after patient search).
/// </summary>
public sealed class CreateWalkinRequest
{
    /// <summary>Patient full name (required, max 200 characters).</summary>
    [Required]
    [MaxLength(200)]
    public required string PatientName { get; init; }

    /// <summary>Contact phone number (optional, 7–20 digits with optional +, spaces, dashes, parens).</summary>
    [RegularExpression(@"^\+?[\d\s\-()+]{7,20}$",
        ErrorMessage = "Phone must be 7–20 characters containing digits, spaces, dashes, or parentheses.")]
    public string? Phone { get; init; }

    /// <summary>Reason for today's visit (required, max 500 characters).</summary>
    [Required]
    [MaxLength(500)]
    public required string VisitReason { get; init; }

    /// <summary>
    /// AC-4: When provided, the walk-in is linked to this existing patient account
    /// without creating a duplicate record. Returns 404 if the patient does not exist.
    /// </summary>
    public Guid? ExistingPatientId { get; init; }

    /// <summary>
    /// AC-2: When true, a new patient account (User + Patient record) is created
    /// from the walk-in demographics. Requires DateOfBirth and Email.
    /// </summary>
    public bool ConvertToPatient { get; init; }

    /// <summary>AC-2: Required when ConvertToPatient is true.</summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>AC-2: Required when ConvertToPatient is true.</summary>
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string? Email { get; init; }
}

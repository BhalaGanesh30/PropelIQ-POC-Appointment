namespace PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

/// <summary>
/// A matching patient record returned by GET /api/v1/patients/search
/// (EP-004 US_033 Edge Case 1).
///
/// Edge Case 1: Multiple patients match the search query; the disambiguation
/// list is returned with enough demographics for staff to identify the correct
/// patient (name, DOB, phone).
/// </summary>
public sealed class PatientSearchResultDto
{
    /// <summary>PK of the patient — used as ExistingPatientId in CreateWalkinRequest.</summary>
    public required Guid PatientId { get; init; }

    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }

    /// <summary>Preferred contact phone from the patient's ContactPreferences JSON column.</summary>
    public string? Phone { get; init; }
}

using PropelIQ.Modules.Scheduling.Application.Walkin.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Patient search contract for walk-in disambiguation (EP-004 US_033 Edge Case 1).
///
/// Implemented by <c>PatientSearchService</c> in the Infrastructure layer.
/// </summary>
public interface IPatientSearchService
{
    /// <summary>
    /// Returns up to 10 patients whose full name or phone number contains
    /// <paramref name="query"/> (case-insensitive).
    ///
    /// Edge Case 1: Multiple matches are returned so the caller can present a
    /// disambiguation list with name, DOB, and phone to identify the correct patient.
    /// </summary>
    /// <param name="query">
    /// Minimum 2-character search string. Matched against first+last name and
    /// the patient's preferred phone number.
    /// </param>
    Task<IReadOnlyList<PatientSearchResultDto>> SearchAsync(
        string query,
        CancellationToken ct = default);
}

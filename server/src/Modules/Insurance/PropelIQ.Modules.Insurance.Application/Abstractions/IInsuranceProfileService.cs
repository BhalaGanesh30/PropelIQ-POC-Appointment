using PropelIQ.Modules.Insurance.Application.Dto;

namespace PropelIQ.Modules.Insurance.Application.Abstractions;

/// <summary>
/// Insurance profile persistence service (EP-005 US_037 AC-3, AC-4; US_038 AC-2).
///
/// Creates or updates an <c>InsuranceProfile</c> record in the database with the
/// validation status returned by <see cref="IInsuranceValidationService"/>.
/// Sensitive fields are encrypted at rest (US_038 AC-1) and decrypted transparently
/// on retrieval (AC-2).
/// </summary>
public interface IInsuranceProfileService
{
    /// <summary>
    /// Persists an insurance profile entry for the given patient and tier.
    /// Encrypts sensitive fields (policy number, provider name, group number) before
    /// writing to the database (US_038 AC-1).
    /// </summary>
    Task<InsuranceSaveResponse> SaveAsync(
        InsuranceSaveRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all insurance profiles for the specified patient with sensitive
    /// fields transparently decrypted (US_038 AC-2).
    /// Returns an empty list when the patient has no insurance records on file.
    /// </summary>
    /// <param name="patientId">UUID of the patient whose profiles to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<InsuranceProfileDto>> GetByPatientIdAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the R2 object key stored for the card image of the specified side
    /// on a given insurance profile (US_038 task_002 AC-1).
    /// </summary>
    /// <param name="profileId">UUID of the insurance profile to update.</param>
    /// <param name="side"><c>front</c> or <c>back</c> (case-insensitive).</param>
    /// <param name="objectKey">R2 object key returned by the storage service upload, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateCardImageKeyAsync(
        Guid profileId,
        string side,
        string? objectKey,
        CancellationToken ct = default);
}


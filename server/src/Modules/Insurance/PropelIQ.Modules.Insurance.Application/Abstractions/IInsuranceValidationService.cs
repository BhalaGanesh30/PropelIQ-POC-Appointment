using PropelIQ.Modules.Insurance.Application.Dto;

namespace PropelIQ.Modules.Insurance.Application.Abstractions;

/// <summary>
/// Insurance soft validation service (EP-005 US_037 AC-1 to AC-4, Edge Cases 1–2).
///
/// AC-1: Validates policy number format and provider code against the reference DB within 500ms.
/// AC-2: Returns <see cref="ValidationStatus.Warning"/> for format mismatches; booking never blocked.
/// AC-3: Records with all checks passing return <see cref="ValidationStatus.SoftValidated"/>.
/// AC-4: Complete failures return <see cref="ValidationStatus.ValidationFailed"/> and are
///       flagged for staff review in the <c>insurance_validation_results</c> table.
/// Edge Case 1: Reference DB unavailable → <see cref="ValidationStatus.ValidationPending"/>;
///              background retry queued.
/// Edge Case 2: Duplicate primary/secondary policy number → advisory warning added.
/// </summary>
public interface IInsuranceValidationService
{
    /// <summary>
    /// Performs a non-blocking soft validation of the supplied insurance details.
    /// Writes one <c>InsuranceValidationResult</c> audit record per call.
    /// </summary>
    /// <param name="request">Insurance details to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Categorised validation response with advisory warnings.</returns>
    Task<InsuranceValidateResponse> ValidateAsync(
        InsuranceValidateRequest request,
        CancellationToken ct = default);
}

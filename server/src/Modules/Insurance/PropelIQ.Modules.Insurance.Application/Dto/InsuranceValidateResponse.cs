namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Response returned by <c>POST /api/v1/insurance/validate</c> (EP-005 US_037 AC-1 to AC-4).
///
/// The validation result is advisory — a <see cref="ValidationStatus.Warning"/> or even
/// a <see cref="ValidationStatus.ValidationFailed"/> status never blocks the booking
/// (AC-2).  The client is expected to persist the profile using
/// <c>POST /api/v1/insurance</c> regardless of the outcome.
/// </summary>
public sealed class InsuranceValidateResponse
{
    /// <summary>Categorised validation outcome.</summary>
    public required ValidationStatus Status { get; init; }

    /// <summary>
    /// Non-blocking advisory messages.  Empty when <see cref="Status"/> is
    /// <see cref="ValidationStatus.SoftValidated"/> with no caveats.
    /// </summary>
    public required List<InsuranceValidationWarning> Warnings { get; init; }

    /// <summary>
    /// True when the submitted <c>ProviderCode</c> was found in the reference database.
    /// False when the provider lookup failed or the DB was unreachable.
    /// </summary>
    public required bool ProviderMatch { get; init; }

    /// <summary>
    /// True when the policy number matched the provider's expected format pattern.
    /// Always true when no pattern is configured for the provider.
    /// False when <see cref="ProviderMatch"/> is false (no pattern to validate against).
    /// </summary>
    public required bool PolicyFormatValid { get; init; }

    /// <summary>
    /// UUID of the <c>InsuranceValidationResult</c> audit record written during this
    /// validation call.  Null only on unexpected internal errors.
    /// </summary>
    public Guid? ValidationResultId { get; init; }
}

namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// A single non-blocking warning attached to a validation response (EP-005 US_037 AC-2).
///
/// Serialised as <c>{ "field": "...", "message": "..." }</c> so the Angular FE model
/// <c>InsuranceValidationWarning</c> can bind directly.
/// </summary>
public sealed record InsuranceValidationWarning
{
    /// <summary>
    /// Form field the warning relates to (e.g. <c>"policyNumber"</c>, <c>"providerCode"</c>).
    /// Empty string for cross-field warnings such as duplicate policy number detection.
    /// </summary>
    public required string Field { get; init; }

    /// <summary>Human-readable warning message to display in the UI.</summary>
    public required string Message { get; init; }
}

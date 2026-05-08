using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Request body for <c>POST /api/v1/insurance/validate</c> (EP-005 US_037 AC-1).
///
/// Used for soft validation only — nothing is persisted by this request.
/// After the client receives the result it calls <c>POST /api/v1/insurance</c>
/// to persist the profile with the resolved <see cref="ValidationStatus"/>.
/// </summary>
public sealed class InsuranceValidateRequest
{
    /// <summary>Patient whose insurance is being validated.</summary>
    [Required]
    public Guid PatientId { get; init; }

    /// <summary>
    /// Insurance policy number (e.g. "ACM-455-9981").
    /// Validated against the provider's <c>PolicyNumberPattern</c> regex (AC-1).
    /// </summary>
    [Required, MinLength(5), MaxLength(30)]
    public string PolicyNumber { get; init; } = string.Empty;

    /// <summary>Short provider lookup code (e.g. "BCBS", "AETNA-TX").</summary>
    [Required, MaxLength(20)]
    public string ProviderCode { get; init; } = string.Empty;

    /// <summary>Human-readable name of the insurance provider.</summary>
    [Required, MaxLength(100)]
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Group number — optional for plans that don't use groups.</summary>
    [MaxLength(30)]
    public string? GroupNumber { get; init; }

    /// <summary>"Primary" or "Secondary".</summary>
    [Required]
    public string Tier { get; init; } = "Primary";

    /// <summary>
    /// Policy number of the primary insurance entry, supplied when validating secondary
    /// insurance so the engine can detect duplicate policy numbers (Edge Case 2).
    /// Null when <see cref="Tier"/> == "Primary".
    /// </summary>
    public string? PrimaryPolicyNumber { get; init; }
}

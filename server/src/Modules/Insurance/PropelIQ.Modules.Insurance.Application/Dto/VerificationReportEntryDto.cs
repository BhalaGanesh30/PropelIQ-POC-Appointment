namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Single row in the insurance verification report (EP-005 US_039 AC-1, AC-3, AC-4).
///
/// Sensitive fields (<see cref="PolicyNumber"/>, <see cref="ProviderName"/>) are
/// decrypted by the service layer before being placed in this DTO.  They MUST NOT
/// be cached in plaintext beyond the HTTP response boundary.
/// </summary>
public sealed record VerificationReportEntryDto
{
    /// <summary>ID of the insurance profile row.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Patient's full name — <c>FirstName LastName</c> from the patients table.</summary>
    public string PatientName { get; init; } = string.Empty;

    /// <summary>Decrypted insurance provider / payer name (US_038 AC-2).</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Decrypted policy number (US_038 AC-1).</summary>
    public string PolicyNumber { get; init; } = string.Empty;

    /// <summary>Validation outcome — serialised as a string for forward-compatibility.</summary>
    public string ValidationStatus { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of the most recent validation attempt.
    /// Sourced from <c>insurance_validation_results.created_at</c> when available,
    /// otherwise falls back to <c>insurance_profiles.updated_at</c>.
    /// </summary>
    public DateTimeOffset ValidatedAt { get; init; }
}

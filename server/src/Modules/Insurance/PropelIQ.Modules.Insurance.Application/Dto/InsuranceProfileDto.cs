namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Decrypted insurance profile returned by <c>GET /api/v1/insurance/{patientId}</c>
/// (EP-005 US_038 AC-2).
///
/// All sensitive fields have been transparently decrypted in the application layer
/// before being returned to the caller.  The API endpoint enforces ownership and
/// role-based access (AC-4) so this DTO is only served to authorised callers.
/// </summary>
public sealed class InsuranceProfileDto
{
    /// <summary>UUID of the insurance profile record.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>UUID of the patient this profile belongs to.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>"Primary" or "Secondary".</summary>
    public required string Tier { get; init; }

    /// <summary>Decrypted policy number.</summary>
    public required string PolicyNumber { get; init; }

    /// <summary>Short provider lookup code (not encrypted — used for lookups).</summary>
    public required string ProviderCode { get; init; }

    /// <summary>Decrypted provider display name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Decrypted group number, or null if not provided.</summary>
    public string? GroupNumber { get; init; }

    /// <summary>Validation status as persisted (e.g. "SoftValidated", "ValidationFailed").</summary>
    public required string ValidationStatus { get; init; }

    /// <summary>
    /// Server-side path to the front card image.
    /// Null when no image was uploaded (Edge Case 2 — not a validation error).
    /// </summary>
    public string? CardImageFrontPath { get; init; }

    /// <summary>
    /// Server-side path to the back card image.
    /// Null when no image was uploaded (Edge Case 2 — not a validation error).
    /// </summary>
    public string? CardImageBackPath { get; init; }
}

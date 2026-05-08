using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Request body for <c>POST /api/v1/insurance</c> (EP-005 US_037 AC-3, AC-4).
///
/// Persists the insurance profile with the <see cref="ValidationStatus"/> returned
/// by the preceding validate call.  Card image paths are optional; when not provided
/// the images were not uploaded in this session.
/// </summary>
public sealed class InsuranceSaveRequest
{
    /// <summary>Patient whose insurance profile is being saved.</summary>
    [Required]
    public Guid PatientId { get; init; }

    /// <summary>Policy number (5–30 alphanumeric characters).</summary>
    [Required, MinLength(5), MaxLength(30)]
    public string PolicyNumber { get; init; } = string.Empty;

    /// <summary>Short provider lookup code.</summary>
    [Required, MaxLength(20)]
    public string ProviderCode { get; init; } = string.Empty;

    /// <summary>Human-readable provider display name.</summary>
    [Required, MaxLength(100)]
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Optional group number.</summary>
    [MaxLength(30)]
    public string? GroupNumber { get; init; }

    /// <summary>"Primary" or "Secondary".</summary>
    [Required]
    public string Tier { get; init; } = "Primary";

    /// <summary>
    /// Validation status returned by the preceding validate call.
    /// Persisted verbatim so staff can filter by status in the review queue (AC-4).
    /// </summary>
    [Required]
    public required string ValidationStatus { get; init; }

    /// <summary>Server-side path to the front card image (if uploaded).</summary>
    [MaxLength(500)]
    public string? CardImageFrontPath { get; init; }

    /// <summary>Server-side path to the back card image (if uploaded).</summary>
    [MaxLength(500)]
    public string? CardImageBackPath { get; init; }
}

using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Administration.Domain.Entities;

/// <summary>
/// Immutable audit record produced each time insurance details are soft-validated
/// (EP-005 US_037 task_002 AC-3, AC-4, Edge Case 1).
///
/// Every call to <c>InsuranceValidationService.ValidateAsync</c> writes one record
/// regardless of the outcome — this feeds the staff review queue (AC-4) and the
/// background retry loop (Edge Case 1).
/// </summary>
public sealed class InsuranceValidationResult : BaseEntity
{
    /// <summary>Patient whose insurance was validated.</summary>
    public required Guid PatientId { get; set; }

    /// <summary>Policy number that was submitted for validation.</summary>
    public required string PolicyNumber { get; set; }

    /// <summary>Provider code submitted by the user.</summary>
    public required string ProviderCode { get; set; }

    /// <summary>"Primary" or "Secondary".</summary>
    public required string Tier { get; set; }

    /// <summary>
    /// Validation outcome stored as a string so that future enum additions do not
    /// require a DB migration.  Valid values: SoftValidated, Warning, ValidationFailed,
    /// ValidationPending.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// JSON-serialised array of warning objects ({ field, message }).
    /// Null when the outcome is SoftValidated with no warnings.
    /// </summary>
    public string? WarningsJson { get; set; }

    /// <summary>
    /// Number of background retry attempts.  Capped at 3 (Edge Case 1).
    /// Only relevant when Status == "ValidationPending".
    /// </summary>
    public int RetryCount { get; set; } = 0;
}

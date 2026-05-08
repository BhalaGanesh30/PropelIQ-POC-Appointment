using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Administration.Domain.Entities;

/// <summary>
/// Reference data record for an insurance provider (EP-005 US_037 task_002).
///
/// The <see cref="PolicyNumberPattern"/> field holds a .NET <see cref="System.Text.RegularExpressions.Regex"/>
/// pattern used by <c>InsuranceValidationService</c> to perform soft format validation
/// against the submitted policy number without querying an external payer gateway (AC-1).
///
/// Records are cached in Redis ("insurance:providers:all", 5-min TTL) so repeated
/// lookups stay well within the 500ms p95 SLA (NFR-002).
/// </summary>
public sealed class InsuranceProvider : BaseEntity
{
    /// <summary>Short unique code used as the lookup key (e.g. "BCBS", "AETNA-TX").</summary>
    public required string ProviderCode { get; set; }

    /// <summary>Human-readable display name.</summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// .NET regex pattern for policy-number format validation.
    /// An empty string means "any format accepted" (no format constraint).
    /// </summary>
    public required string PolicyNumberPattern { get; set; }

    /// <summary>
    /// Soft-delete flag.  Inactive providers are excluded from new validations
    /// but records that reference them are not cascaded.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

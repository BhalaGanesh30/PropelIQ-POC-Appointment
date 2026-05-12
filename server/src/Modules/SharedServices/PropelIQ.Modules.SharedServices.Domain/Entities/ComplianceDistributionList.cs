using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// An individual recipient in the compliance report distribution list (US_058, AC-3).
///
/// Maps to <c>compliance.compliance_distribution_lists</c> (created by US_058 task_002 migration).
/// </summary>
public sealed class ComplianceDistributionList : BaseEntity
{
    /// <summary>Recipient display name.</summary>
    public required string Name     { get; set; }

    /// <summary>Recipient email address. Validated at creation.</summary>
    public required string Email    { get; set; }

    /// <summary>Whether this recipient should receive generated reports.</summary>
    public bool IsActive { get; set; } = true;
}

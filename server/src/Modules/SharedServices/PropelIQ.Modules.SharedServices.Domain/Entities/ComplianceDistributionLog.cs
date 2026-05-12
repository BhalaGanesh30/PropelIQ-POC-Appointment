namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Immutable delivery-attempt log for compliance report distribution (US_058, AC-3, edge case 2).
///
/// Maps to <c>compliance.compliance_distribution_log</c> (created by US_058 task_002 migration).
/// A new row is written for each send attempt; no BaseEntity — append-only, no updates.
/// </summary>
public sealed class ComplianceDistributionLog
{
    public Guid     Id              { get; init; } = Guid.NewGuid();

    /// <summary>FK to the <see cref="ComplianceReportRecord"/> being distributed.</summary>
    public required Guid     ReportId        { get; init; }

    /// <summary>FK to the <see cref="ComplianceDistributionList"/> recipient.</summary>
    public required Guid     RecipientId     { get; init; }

    /// <summary>Recipient email address at the time of delivery (denormalised for log integrity).</summary>
    public required string   RecipientEmail  { get; init; }

    /// <summary>Delivery status: Sent | Failed | Retried.</summary>
    public required string   Status          { get; init; }

    /// <summary>UTC timestamp of this delivery attempt.</summary>
    public required DateTime AttemptedAtUtc  { get; init; }

    /// <summary>Attempt number (1 = initial, 2 = retry). Max 2 per Polly retry policy.</summary>
    public required int      AttemptNumber   { get; init; }

    /// <summary>Error details when <see cref="Status"/> is Failed. Null on success.</summary>
    public string? ErrorDetail { get; init; }
}

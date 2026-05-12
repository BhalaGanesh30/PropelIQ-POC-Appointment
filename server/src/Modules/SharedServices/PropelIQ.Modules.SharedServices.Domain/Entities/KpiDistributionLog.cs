namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Append-only log of KPI report distribution events (US_060, AC-4).
///
/// One row is written per distribution attempt.
/// Maps to <c>app.kpi_distribution_logs</c> (created by US_060 task_002 migration).
/// </summary>
public sealed class KpiDistributionLog
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Start of the report period covered by the distributed PDF.</summary>
    public required DateOnly PeriodFrom { get; init; }

    /// <summary>End of the report period covered by the distributed PDF.</summary>
    public required DateOnly PeriodTo { get; init; }

    /// <summary>Comma-separated list of recipient email addresses.</summary>
    public required string RecipientEmails { get; init; }

    /// <summary>Delivery status: <c>Sent</c> or <c>Failed</c>.</summary>
    public required string Status { get; set; }

    /// <summary>UTC timestamp of this distribution attempt.</summary>
    public DateTime SentAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Error detail when <see cref="Status"/> is <c>Failed</c>. Null on success.</summary>
    public string? ErrorDetail { get; set; }
}

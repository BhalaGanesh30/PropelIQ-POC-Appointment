namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Tracks the state of an asynchronous compliance report generation job (US_058, edge case 1).
///
/// Maps to <c>compliance.compliance_report_jobs</c> (created by US_058 task_002 migration).
/// A row is created when the controller receives a large date range and returns 202 Accepted.
/// </summary>
public sealed class ComplianceReportJob
{
    public Guid     Id              { get; init; } = Guid.NewGuid();

    /// <summary>FK to the pre-allocated <see cref="ComplianceReportRecord"/> row.</summary>
    public required Guid     ReportId        { get; init; }

    /// <summary>Admin user who triggered the on-demand request. Used for completion email.</summary>
    public required Guid     RequestedBy     { get; init; }

    /// <summary>Serialised <c>ReportRequest</c> JSON so the worker can rebuild the request.</summary>
    public required string   RequestJson     { get; init; }

    /// <summary>Job status: Queued | Generating | Completed | Failed.</summary>
    public string Status { get; set; } = "Queued";

    /// <summary>UTC timestamp when the job was created.</summary>
    public required DateTime CreatedAtUtc    { get; init; }

    /// <summary>UTC timestamp when the job completed or failed. Null while running.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Error message when <see cref="Status"/> is Failed. Null on success.</summary>
    public string? ErrorMessage { get; set; }
}

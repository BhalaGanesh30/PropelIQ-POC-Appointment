using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Defines a recurring compliance report schedule (US_058, AC-1).
///
/// Maps to <c>compliance.compliance_report_schedules</c> (created by US_058 task_002 migration).
/// Supported recurrence patterns: <c>Daily</c>, <c>Weekly</c>, <c>Monthly</c>.
/// </summary>
public sealed class ComplianceReportSchedule : BaseEntity
{
    /// <summary>Human-readable label for this schedule.</summary>
    public required string   Name            { get; set; }

    /// <summary>Machine-readable report type: HIPAA | AccessSummary | AuditEventSummary.</summary>
    public required string   ReportType      { get; set; }

    /// <summary>Recurrence pattern: Daily | Weekly | Monthly.</summary>
    public required string   Recurrence      { get; set; }

    /// <summary>Whether this schedule is active and should be evaluated by the worker.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp of the most recent successful generation. Null if never run.</summary>
    public DateTime? LastRunAt  { get; set; }

    /// <summary>UTC timestamp of the next scheduled run. Null until first calculation.</summary>
    public DateTime? NextRunAt  { get; set; }
}

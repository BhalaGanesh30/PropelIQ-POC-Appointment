using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Persisted compliance report record storing metadata and the rendered PDF (US_058, AC-1, AC-2).
///
/// Maps to <c>compliance.compliance_reports</c> (created by US_058 task_002 migration).
/// </summary>
public sealed class ComplianceReportRecord : BaseEntity
{
    /// <summary>Machine-readable report type: HIPAA | AccessSummary | AuditEventSummary.</summary>
    public required string   ReportType      { get; set; }

    /// <summary>Inclusive start of the period covered by this report (UTC).</summary>
    public required DateTime PeriodStartUtc  { get; set; }

    /// <summary>Inclusive end of the period covered by this report (UTC).</summary>
    public required DateTime PeriodEndUtc    { get; set; }

    /// <summary>UTC timestamp when generation completed.</summary>
    public required DateTime GeneratedAtUtc  { get; set; }

    // ── Metrics snapshot (denormalised for fast list queries) ──────────────────
    public int TotalAuditEvents     { get; set; }
    public int UniqueActors         { get; set; }
    public int AnomalyCount         { get; set; }
    public int FailedAccessAttempts { get; set; }

    /// <summary>Rendered PDF bytes. Null while the async job is still running.</summary>
    public byte[]? PdfContent { get; set; }

    /// <summary>
    /// Processing status: <c>Completed</c> | <c>Generating</c> | <c>Failed</c>.
    /// </summary>
    public string Status { get; set; } = "Generating";

    /// <summary>True when this report was generated asynchronously (edge case 1).</summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// FK to the async job entry. Null for synchronous reports.
    /// </summary>
    public Guid? JobId { get; set; }
}

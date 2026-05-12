namespace PropelIQ.Modules.SharedServices.Application.Compliance;

// ── Request ───────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for on-demand compliance report generation (US_058, AC-4).
///
/// Validated by <c>ReportRequestValidator</c> before reaching the service.
/// </summary>
public sealed record ReportRequest
{
    /// <summary>
    /// Machine-readable report type identifier.
    /// Supported values: <c>HIPAA</c>, <c>AccessSummary</c>, <c>AuditEventSummary</c>.
    /// </summary>
    public required string   ReportType       { get; init; }

    /// <summary>Inclusive start of the reporting period (UTC).</summary>
    public required DateTime PeriodStartUtc   { get; init; }

    /// <summary>Inclusive end of the reporting period (UTC). Must be after PeriodStartUtc.</summary>
    public required DateTime PeriodEndUtc     { get; init; }

    /// <summary>
    /// Optional: admin user ID making the request, used in async-job completion notification.
    /// Null for schedule-triggered generation.
    /// </summary>
    public Guid? RequestedByUserId { get; init; }
}

// ── Valid report types ─────────────────────────────────────────────────────────

/// <summary>Centralised list of supported report type constants.</summary>
public static class ReportTypes
{
    public const string Hipaa              = "HIPAA";
    public const string AccessSummary      = "AccessSummary";
    public const string AuditEventSummary  = "AuditEventSummary";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Hipaa, AccessSummary, AuditEventSummary };
}

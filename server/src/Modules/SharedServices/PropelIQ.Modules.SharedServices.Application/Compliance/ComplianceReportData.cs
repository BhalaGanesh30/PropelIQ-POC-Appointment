namespace PropelIQ.Modules.SharedServices.Application.Compliance;

// ── Report data model ─────────────────────────────────────────────────────────

/// <summary>
/// Fully-aggregated, in-memory representation of a HIPAA compliance report (US_058, AC-1, AC-2).
///
/// Produced by <c>ComplianceReportGenerator</c> and consumed by
/// <c>ComplianceReportPdfRenderer</c> and the persistence layer.
/// </summary>
public sealed record ComplianceReportData
{
    public required Guid         ReportId        { get; init; }
    public required string       ReportType      { get; init; }
    public required DateTime     PeriodStartUtc  { get; init; }
    public required DateTime     PeriodEndUtc    { get; init; }
    public required DateTime     GeneratedAtUtc  { get; init; }
    public required AccessLogSummary         AccessSummary { get; init; }
    public required IReadOnlyList<EventTypeCount> EventCounts { get; init; }
    public required IReadOnlyList<AnomalyFlag>    Anomalies   { get; init; }
    public required ReportMetrics            KeyMetrics  { get; init; }
}

// ── Access log summary sub-records ────────────────────────────────────────────

/// <summary>
/// Summarises all DataAccess events in the reporting period, grouped by actor and resource.
/// </summary>
public sealed record AccessLogSummary
{
    public required int TotalAccessEvents { get; init; }
    public required IReadOnlyList<ActorAccessGroup>    ByActor    { get; init; }
    public required IReadOnlyList<ResourceAccessGroup> ByResource { get; init; }
}

/// <summary>Access counts aggregated by a single actor for the reporting period.</summary>
public sealed record ActorAccessGroup
{
    public required string ActorName    { get; init; }
    public required string Role         { get; init; }
    public required int    AccessCount  { get; init; }
}

/// <summary>Access counts aggregated by resource / entity type for the reporting period.</summary>
public sealed record ResourceAccessGroup
{
    public required string ResourceType { get; init; }
    public required int    AccessCount  { get; init; }
}

// ── Event counts ──────────────────────────────────────────────────────────────

/// <summary>Count of audit records with a given <c>EventType</c> for the reporting period.</summary>
public sealed record EventTypeCount
{
    public required string EventType { get; init; }
    public required int    Count     { get; init; }
}

// ── Anomaly detection ─────────────────────────────────────────────────────────

/// <summary>
/// Represents a detected anomaly within the reporting period.
///
/// Anomaly types: <c>UnusualAccessVolume</c>, <c>OffHoursAccess</c>, <c>RepeatedFailedAttempts</c>.
/// Severity values: <c>Low</c>, <c>Medium</c>, <c>High</c>.
/// </summary>
public sealed record AnomalyFlag
{
    public required string   AnomalyType    { get; init; }
    public required string   Description    { get; init; }
    public required string   Severity       { get; init; }
    public required DateTime DetectedAtUtc  { get; init; }
}

// ── Key metrics ───────────────────────────────────────────────────────────────

/// <summary>High-level metrics shown in the executive summary section of the PDF (AC-2).</summary>
public sealed record ReportMetrics
{
    public required int TotalAuditEvents       { get; init; }
    public required int UniqueActors           { get; init; }
    public required int AnomalyCount           { get; init; }
    public required int FailedAccessAttempts   { get; init; }
}

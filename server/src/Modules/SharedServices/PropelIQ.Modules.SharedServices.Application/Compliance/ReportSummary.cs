namespace PropelIQ.Modules.SharedServices.Application.Compliance;

// ── List response ─────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight read model for compliance report list endpoints (US_058, AC-2).
///
/// Intentionally excludes the PDF bytes — those are fetched via the dedicated download endpoint.
/// </summary>
public sealed record ReportSummary
{
    public required Guid     Id              { get; init; }
    public required string   ReportType      { get; init; }
    public required DateTime PeriodStartUtc  { get; init; }
    public required DateTime PeriodEndUtc    { get; init; }
    public required DateTime GeneratedAtUtc  { get; init; }
    public required int      TotalAuditEvents   { get; init; }
    public required int      UniqueActors        { get; init; }
    public required int      AnomalyCount        { get; init; }
    public required bool     IsAsync         { get; init; }
    public required string   Status          { get; init; }  // Completed | Generating | Failed
}

// ── Paginated wrapper ─────────────────────────────────────────────────────────

/// <summary>Generic paginated result envelope used by the list endpoint.</summary>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items     { get; init; }
    public required int              Total     { get; init; }
    public required int              Page      { get; init; }
    public required int              PageSize  { get; init; }
}

// ── Generation result ─────────────────────────────────────────────────────────

/// <summary>
/// Returned from <see cref="IComplianceReportService.GenerateAsync"/> to signal
/// whether the report completed synchronously or was queued for async processing.
/// </summary>
public sealed record ReportGenerationResult
{
    /// <summary>Report ID (set for synchronous completion).</summary>
    public required Guid Id      { get; init; }

    /// <summary>True when the report was too large and queued asynchronously (edge case 1).</summary>
    public required bool IsAsync { get; init; }

    /// <summary>Job tracking ID when <see cref="IsAsync"/> is true. Null otherwise.</summary>
    public Guid? JobId { get; init; }
}

// ── Job status ────────────────────────────────────────────────────────────────

/// <summary>
/// Async job status polled via <c>GET /api/v1/admin/reports/{id}/status</c> (edge case 1).
/// </summary>
public sealed record ReportJobStatus
{
    public required Guid    JobId       { get; init; }
    public required string  Status      { get; init; }  // Queued | Generating | Completed | Failed
    public Guid?            ReportId    { get; init; }
    public DateTime?        CompletedAt { get; init; }
    public string?          ErrorMessage{ get; init; }
}

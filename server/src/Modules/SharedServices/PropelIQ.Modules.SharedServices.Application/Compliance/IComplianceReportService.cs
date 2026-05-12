namespace PropelIQ.Modules.SharedServices.Application.Compliance;

/// <summary>
/// Application service contract for HIPAA compliance report lifecycle (US_058, AC-1–AC-4).
///
/// Implemented by <c>ComplianceReportService</c> in the Infrastructure layer.
/// Consumed by <c>ComplianceReportController</c> (REST), <c>ComplianceReportScheduleWorker</c>
/// (scheduled), and <c>ComplianceReportJobWorker</c> (async channel).
/// </summary>
public interface IComplianceReportService
{
    /// <summary>
    /// Generates a compliance report for the given date range (AC-4).
    ///
    /// When the date span exceeds 90 days (2-minute heuristic threshold), the report is
    /// queued asynchronously via the bounded channel and a 202-result is indicated
    /// by <see cref="ReportGenerationResult.IsAsync"/> = true (edge case 1).
    /// </summary>
    Task<ReportGenerationResult> GenerateAsync(
        ReportRequest       request,
        CancellationToken   ct = default);

    /// <summary>
    /// Returns a paginated list of compliance report summaries (AC-2).
    /// </summary>
    Task<PagedResult<ReportSummary>> ListAsync(
        int               page,
        int               pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns metadata for a single compliance report. Null when not found.
    /// </summary>
    Task<ReportSummary?> GetAsync(
        Guid              reportId,
        CancellationToken ct = default);

    /// <summary>
    /// Opens the stored PDF for streaming download (AC-2). Null when not found.
    /// </summary>
    Task<Stream?> DownloadPdfAsync(
        Guid              reportId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the processing status for an async report job (edge case 1). Null when not found.
    /// </summary>
    Task<ReportJobStatus?> GetJobStatusAsync(
        Guid              jobId,
        CancellationToken ct = default);
}

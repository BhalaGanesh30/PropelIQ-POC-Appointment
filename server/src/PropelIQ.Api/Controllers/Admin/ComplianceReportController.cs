using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.SharedServices.Application.Compliance;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin-only compliance report REST API (US_058, AC-1–AC-4, edge cases 1–2).
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>POST /api/v1/admin/reports</c>           — trigger on-demand report generation (AC-4).</item>
///   <item><c>GET  /api/v1/admin/reports</c>           — paginated list of generated reports (AC-2).</item>
///   <item><c>GET  /api/v1/admin/reports/{id}</c>       — fetch individual report metadata.</item>
///   <item><c>GET  /api/v1/admin/reports/{id}/download</c> — download PDF (AC-2).</item>
///   <item><c>GET  /api/v1/admin/reports/{id}/status</c>   — poll async job status (edge case 1).</item>
/// </list>
///
/// AC-4 SLA: reports with date range ≤ 90 days return 200 immediately.
/// Reports with range &gt; 90 days return 202 Accepted with a <c>jobId</c> for polling.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/reports")]
[ApiController]
[Produces("application/json")]
public sealed class ComplianceReportController : BaseApiController
{
    private readonly IComplianceReportService _service;

    public ComplianceReportController(IComplianceReportService service)
        => _service = service;

    /// <summary>
    /// Triggers on-demand compliance report generation.
    ///
    /// Returns <c>200 OK</c> with <c>{ id, status="Completed" }</c> for quick reports.
    /// Returns <c>202 Accepted</c> with <c>{ jobId, status="Generating" }</c> for async jobs
    /// where the date range exceeds 90 days (edge case 1).
    ///
    /// FluentValidation auto-validates <see cref="ReportRequest"/> before the action runs.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate(
        [FromBody] ReportRequest request,
        CancellationToken ct)
    {
        var result = await _service.GenerateAsync(request, ct);

        if (result.IsAsync)
        {
            return Accepted(new
            {
                result.JobId,
                Status = "Generating",
            });
        }

        return Ok(new
        {
            result.Id,
            Status = "Completed",
        });
    }

    /// <summary>
    /// Returns a paginated list of all generated compliance reports, most recent first (AC-2).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReportSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct     = default)
    {
        var result = await _service.ListAsync(page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns metadata for a single compliance report.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReportSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var report = await _service.GetAsync(id, ct);
        return report is null ? NotFound() : Ok(report);
    }

    /// <summary>
    /// Streams the PDF file for a completed compliance report (AC-2).
    ///
    /// Returns <c>404 Not Found</c> when the report does not exist or PDF generation
    /// is not yet complete (async jobs).
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var stream = await _service.DownloadPdfAsync(id, ct);
        if (stream is null) return NotFound();

        return File(stream, "application/pdf", $"compliance-report-{id}.pdf");
    }

    /// <summary>
    /// Returns the current status of an async report job (edge case 1).
    ///
    /// Frontend polls this endpoint using the <c>jobId</c> returned by <c>POST /reports</c>
    /// to drive the progress indicator until status transitions to "Completed" or "Failed".
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ReportJobStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        var status = await _service.GetJobStatusAsync(id, ct);
        return status is null ? NotFound() : Ok(status);
    }
}

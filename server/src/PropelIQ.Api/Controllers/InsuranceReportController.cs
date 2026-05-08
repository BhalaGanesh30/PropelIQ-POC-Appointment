using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Application.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Insurance Verification Report API (EP-005 US_039).
///
/// All three endpoints are restricted to <b>Staff and Admin</b> roles (Edge Case 2, AC-4).
/// Patient role receives HTTP 403.
///
/// GET /api/v1/insurance-report
///   Returns a paginated, optionally filtered list of insurance verification records
///   with Redis caching (30s TTL) to meet the 500ms p95 target (AC-2, NFR-002).
///
/// GET /api/v1/insurance-report/export/pdf
///   Generates and downloads a QuestPDF A4 report of ALL filtered records (AC-3).
///
/// GET /api/v1/insurance-report/export/csv
///   Generates and downloads a CsvHelper CSV file for billing system import (AC-4).
///
/// Export endpoints stream the full filtered result set regardless of pagination
/// state in the listing view (Edge Case 1).
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[ApiController]
[Route("api/v1/insurance-report")]
[Produces("application/json")]
public sealed class InsuranceReportController : BaseApiController
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Insurance.InsuranceReportController");

    private readonly IInsuranceReportService _reportService;

    public InsuranceReportController(IInsuranceReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Returns a paginated list of insurance verification records, optionally filtered
    /// by validation status (AC-1, AC-2).
    ///
    /// Status filter and pagination parameters are passed as query string arguments.
    /// Results are Redis-cached (30s TTL) to meet NFR-002 500ms p95 (AC-2).
    /// </summary>
    /// <param name="filter">Pagination and filter parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of verification records with total count for pagination.</returns>
    /// <response code="200">Successfully retrieved the paged report.</response>
    /// <response code="401">JWT token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role (Edge Case 2).</response>
    [HttpGet]
    [ProducesResponseType(typeof(VerificationReportPagedResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReport(
        [FromQuery] VerificationReportFilterDto filter,
        CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("GetReport");

        var result = await _reportService.GetPagedReportAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Exports ALL filtered insurance verification records as a PDF file (AC-3).
    ///
    /// The generated PDF is formatted for A4 paper with status colour-coding,
    /// alternating row backgrounds, and page numbers.  The file includes ALL records
    /// matching the status filter — not just the current page view (Edge Case 1).
    /// </summary>
    /// <param name="status">Optional validation status filter (null = all statuses).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>application/pdf</c> file download named
    /// <c>insurance-verification-report.pdf</c>.
    /// </returns>
    /// <response code="200">PDF file as binary download.</response>
    /// <response code="401">JWT token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role (Edge Case 2).</response>
    [HttpGet("export/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] ValidationStatus? status,
        CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("ExportPdf");
        activity?.SetTag("filter.status", status?.ToString() ?? "all");

        var pdf = await _reportService.GeneratePdfAsync(status, ct);

        return File(
            pdf,
            "application/pdf",
            "insurance-verification-report.pdf");
    }

    /// <summary>
    /// Exports ALL filtered insurance verification records as a CSV file (AC-4).
    ///
    /// CSV columns: PatientName, ProviderName, PolicyNumber, ValidationStatus, ValidatedAt.
    /// The file is formatted per RFC 4180 (CRLF line endings) for billing system import.
    /// The export includes ALL records matching the status filter (Edge Case 1).
    /// </summary>
    /// <param name="status">Optional validation status filter (null = all statuses).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>text/csv</c> file download named
    /// <c>insurance-verification-report.csv</c>.
    /// </returns>
    /// <response code="200">CSV file as binary download.</response>
    /// <response code="401">JWT token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role (Edge Case 2).</response>
    [HttpGet("export/csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] ValidationStatus? status,
        CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("ExportCsv");
        activity?.SetTag("filter.status", status?.ToString() ?? "all");

        var csv = await _reportService.GenerateCsvAsync(status, ct);

        return File(
            csv,
            "text/csv",
            "insurance-verification-report.csv");
    }
}

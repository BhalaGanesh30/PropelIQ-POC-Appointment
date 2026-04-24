using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Appointments.Dto;
using PropelIQ.Modules.Scheduling.Infrastructure.Appointments;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Appointment history API (US_025).
///
/// GET  /api/v1/appointmenthistory         — paginated history with optional filters (AC-1..3).
/// GET  /api/v1/appointmenthistory/export  — download full history as PDF (AC-4).
///
/// Patient ownership is enforced by extracting PatientId from the authenticated
/// NameIdentifier claim — no patient can access another patient's records.
/// All endpoints require JWT bearer authentication.
/// </summary>
[Authorize]
public sealed class AppointmentHistoryController : BaseApiController
{
    private readonly AppointmentHistoryService _historyService;

    public AppointmentHistoryController(AppointmentHistoryService historyService)
        => _historyService = historyService;

    private Guid GetPatientId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns a paginated, filtered appointment history for the authenticated patient.
    /// Results are sorted date descending (AC-1).
    /// Optional status filter must be one of: Confirmed, Completed, Cancelled, NoShow, Rescheduled (AC-2).
    /// Optional date-range filter applied inclusive on both ends (AC-3).
    /// Edge case: empty history returns 200 with an empty array and totalCount=0.
    /// </summary>
    /// <param name="filter">Query parameters: status, dateFrom, dateTo, page, pageSize.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Paginated appointment history (may be empty).</response>
    /// <response code="400">Validation failure — invalid status, date range, or pagination parameters.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AppointmentHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        var result = await _historyService.GetHistoryAsync(GetPatientId(), filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Downloads a PDF containing all filtered appointments for the authenticated patient (AC-4).
    /// All active query filters (status, dateFrom, dateTo) are applied.
    /// Pagination is ignored — the PDF contains the complete result set.
    /// Edge case: empty result set produces a PDF with a "No appointments found" message.
    /// </summary>
    /// <param name="filter">Query parameters: status, dateFrom, dateTo (pagination ignored for PDF).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">PDF file download.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    [HttpGet("export")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        var pdfBytes = await _historyService.ExportPdfAsync(GetPatientId(), filter, ct);

        return File(
            pdfBytes,
            "application/pdf",
            $"appointment-history-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }
}

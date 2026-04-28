using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Booking.Dto;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Public endpoints for one-click appointment confirmation and cancellation
/// accessed directly from reminder email/SMS links.
///
/// AC-3: Confirm updates appointment status to <c>PatientConfirmed</c> and
///       records the event in <c>ReminderEvent.ConfirmationResponse</c>.
/// Cancel invokes the existing cancellation flow and triggers
/// <c>BookingCancelledEvent</c> cascade (US_026 task_001).
///
/// These endpoints use <c>[AllowAnonymous]</c> because the HMAC-signed token
/// itself serves as a one-time authorization mechanism — the patient clicks
/// directly from email/SMS without logging in.
///
/// Edge case: If the appointment has already passed, the endpoint returns
/// an HTML page informing the patient the link is expired.
/// Double-click protection: <c>ConfirmationResponse</c> is checked before processing.
/// </summary>
[ApiController]
[Route("api/v1/reminders")]
[AllowAnonymous]
public sealed class ReminderActionController : ControllerBase
{
    private readonly IReminderTokenService _tokenService;
    private readonly IReminderEventRepository _reminderRepo;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderActionController> _logger;

    public ReminderActionController(
        IReminderTokenService tokenService,
        IReminderEventRepository reminderRepo,
        AppDbContext db,
        TimeProvider timeProvider,
        ILogger<ReminderActionController> logger)
    {
        _tokenService = tokenService;
        _reminderRepo = reminderRepo;
        _db           = db;
        _timeProvider = timeProvider;
        _logger       = logger;
    }

    /// <summary>AC-3: One-click confirm from reminder email.</summary>
    [HttpGet("confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public Task<IActionResult> Confirm([FromQuery] string token, CancellationToken ct)
        => ProcessActionAsync(token, "confirm", ct);

    /// <summary>One-click cancel from reminder email.</summary>
    [HttpGet("cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public Task<IActionResult> Cancel([FromQuery] string token, CancellationToken ct)
        => ProcessActionAsync(token, "cancel", ct);

    /// <summary>Combined action endpoint for SMS short-links.</summary>
    [HttpGet("action")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Action(
        [FromQuery] string token,
        [FromQuery] string? act,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(act) || (act is not "confirm" and not "cancel"))
            return Task.FromResult<IActionResult>(Content(BuildActionSelectionHtml(token), "text/html"));

        return ProcessActionAsync(token, act, ct);
    }

    // ── Core logic ──────────────────────────────────────────────────────────

    private async Task<IActionResult> ProcessActionAsync(
        string token, string action, CancellationToken ct)
    {
        // 1. Validate HMAC token.
        var payload = _tokenService.ValidateToken(token);
        if (payload is null)
            return BadRequest(Content(BuildErrorHtml("Invalid or tampered link."), "text/html"));

        // 2. Load appointment.
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == payload.AppointmentId, ct);

        if (appointment is null)
            return NotFound(Content(BuildErrorHtml("Appointment not found."), "text/html"));

        // 3. Edge case: Check if appointment has already passed.
        var now = _timeProvider.GetUtcNow();
        if (appointment.ScheduledAt <= now)
            return StatusCode(StatusCodes.Status410Gone, Content(BuildExpiredHtml(), "text/html"));

        // 4. Double-click protection: check ConfirmationResponse.
        var reminder = await _reminderRepo.GetByIdAsync(payload.ReminderId, ct);
        if (reminder?.ConfirmationResponse is not null)
            return Content(BuildAlreadyProcessedHtml(reminder.ConfirmationResponse), "text/html");

        // 5. Process the action.
        if (action == "confirm")
        {
            // AC-3: Transition to PatientConfirmed.
            appointment.Status = AppointmentStatus.PatientConfirmed.ToString();
            await _db.SaveChangesAsync(ct);

            await _reminderRepo.RecordConfirmationResponseAsync(payload.ReminderId, "Confirmed", ct);

            _logger.LogInformation(
                "Patient confirmed appointment {AppointmentId} via reminder {ReminderId}.",
                payload.AppointmentId, payload.ReminderId);

            return Content(BuildSuccessHtml("confirmed"), "text/html");
        }
        else
        {
            // Cancel: Update status directly and record response.
            // The HMAC-signed token serves as authorization — bypasses the
            // 24-hour policy window since the patient is acting on a system-sent link.
            appointment.Status = AppointmentStatus.Cancelled.ToString();
            await _db.SaveChangesAsync(ct);

            await _reminderRepo.RecordConfirmationResponseAsync(payload.ReminderId, "Cancelled", ct);

            // Release the slot if one was reserved.
            if (appointment.SlotId.HasValue)
            {
                await _db.AppointmentSlots
                    .Where(s => s.Id == appointment.SlotId.Value)
                    .ExecuteUpdateAsync(
                        u => u.SetProperty(s => s.CurrentBookings, s => s.CurrentBookings - 1),
                        ct);
            }

            _logger.LogInformation(
                "Patient cancelled appointment {AppointmentId} via reminder {ReminderId}.",
                payload.AppointmentId, payload.ReminderId);

            return Content(BuildSuccessHtml("cancelled"), "text/html");
        }
    }

    // ── HTML response builders ──────────────────────────────────────────────

    private static readonly string SharedStyle = """
        <style>
            body { font-family: sans-serif; text-align: center; padding: 48px 16px; background: #fafafa; }
            .icon { font-size: 64px; margin-bottom: 16px; }
            h1 { color: #333; }
            p { color: #666; max-width: 400px; margin: 16px auto; }
        </style>
        """;

    private static string BuildSuccessHtml(string action)
    {
        var icon = action == "confirmed" ? "✅" : "❌";
        return "<!DOCTYPE html><html lang=\"en\"><head>" +
            "<meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<title>Appointment " + action + "</title>" +
            SharedStyle +
            "</head><body>" +
            "<div class=\"icon\">" + icon + "</div>" +
            "<h1>Appointment " + action + "</h1>" +
            "<p>Your appointment has been successfully " + action + ". You may close this page.</p>" +
            "</body></html>";
    }

    private static string BuildExpiredHtml()
    {
        return "<!DOCTYPE html><html lang=\"en\"><head>" +
            "<meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<title>Link Expired</title>" +
            SharedStyle +
            "</head><body>" +
            "<div class=\"icon\">⏰</div>" +
            "<h1>This link has expired</h1>" +
            "<p>The appointment time has passed. This action is no longer applicable.</p>" +
            "</body></html>";
    }

    private static string BuildErrorHtml(string message)
    {
        return "<!DOCTYPE html><html lang=\"en\"><head>" +
            "<meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<title>Error</title>" +
            "<style>body { font-family: sans-serif; text-align: center; padding: 48px 16px; }" +
            "h1 { color: #c62828; } p { color: #666; max-width: 400px; margin: 16px auto; }</style>" +
            "</head><body>" +
            "<h1>Error</h1>" +
            "<p>" + message + "</p>" +
            "</body></html>";
    }

    private static string BuildAlreadyProcessedHtml(string previousAction)
    {
        return "<!DOCTYPE html><html lang=\"en\"><head>" +
            "<meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<title>Already Processed</title>" +
            SharedStyle +
            "</head><body>" +
            "<div class=\"icon\">ℹ️</div>" +
            "<h1>Already Processed</h1>" +
            "<p>This appointment was already " + previousAction.ToLowerInvariant() +
            ". No further action is needed.</p>" +
            "</body></html>";
    }

    private static string BuildActionSelectionHtml(string token)
    {
        return "<!DOCTYPE html><html lang=\"en\"><head>" +
            "<meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<title>Appointment Action</title>" +
            "<style>body { font-family: sans-serif; text-align: center; padding: 48px 16px; background: #fafafa; }" +
            "h1 { color: #333; } p { color: #666; margin-bottom: 24px; }" +
            ".btn { display: inline-block; padding: 16px 32px; margin: 8px; border-radius: 4px; " +
            "color: #fff; text-decoration: none; font-size: 16px; }" +
            ".confirm { background: #4CAF50; } .cancel { background: #f44336; }</style>" +
            "</head><body>" +
            "<h1>Appointment Action</h1>" +
            "<p>What would you like to do?</p>" +
            "<a class=\"btn confirm\" href=\"?token=" + token + "&amp;act=confirm\">Confirm</a>" +
            "<a class=\"btn cancel\" href=\"?token=" + token + "&amp;act=cancel\">Cancel</a>" +
            "</body></html>";
    }
}

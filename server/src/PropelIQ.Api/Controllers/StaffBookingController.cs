using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Staff-Assisted Booking API (EP-004 US_035 FR-SO-005).
///
/// POST /api/v1/staff-bookings — creates an appointment on behalf of a patient without
/// requiring patient-side verification. The booking is attributed to the acting staff member.
///
/// GET /api/v1/appointments/conflict-check — detects scheduling conflicts before the
/// client submits the booking, allowing staff to collect an override reason.
///
/// AC-1: Booking is created without patient verification requirements.
/// AC-2: <c>staffActorId</c> in the response identifies the booking creator.
/// AC-3: <c>NewPatient</c> payload creates a patient profile inline.
/// AC-4: Every booking produces an immutable <c>StaffBooking</c> audit record.
/// Edge Case 1: HTTP 409 returned when a conflict exists and no override reason is provided.
/// Edge Case 2: HTTP 400 returned when staff attempts to book for themselves.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
[ApiController]
[Produces("application/json")]
public sealed class StaffBookingController : BaseApiController
{
    private readonly IStaffBookingService _staffBookingService;

    public StaffBookingController(IStaffBookingService staffBookingService)
    {
        _staffBookingService = staffBookingService;
    }

    /// <summary>
    /// Creates an appointment on behalf of a patient (staff-assisted booking).
    /// </summary>
    /// <param name="request">
    /// Booking payload: patient ID or inline patient form, slot ID,
    /// visit reason, and optional override reason if a conflict was acknowledged.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Booking IDs and staff actor attribution.</returns>
    /// <response code="201">Booking created; appointment ID and staff actor ID returned.</response>
    /// <response code="400">
    /// Validation failure — missing or mutually exclusive patient/slot fields,
    /// or self-booking attempt detected.
    /// </response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    /// <response code="404">Patient or slot not found.</response>
    /// <response code="409">
    /// Scheduling conflict detected and no override reason supplied.
    /// Response body contains the conflicting appointment details.
    /// </response>
    [HttpPost("/api/v1/staff-bookings")]
    [ProducesResponseType(typeof(StaffBookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ConflictCheckResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBooking(
        [FromBody] CreateStaffBookingRequest request,
        CancellationToken ct)
    {
        var staffUserId = TryGetCurrentUserId();
        if (staffUserId is null)
            return Unauthorized();

        try
        {
            var response = await _staffBookingService.CreateBookingAsync(
                request, staffUserId.Value, ct);

            return CreatedAtAction(
                nameof(CreateBooking),
                new { id = response.BookingId },
                response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (SlotConflictException ex)
        {
            // Edge Case 1: return 409 with conflict details so the client can
            // collect an override reason and re-submit.
            return Conflict(ex.ConflictDetails);
        }
    }

    /// <summary>
    /// Checks whether the given patient has a confirmed appointment that overlaps the
    /// requested slot. Call this before booking to detect conflicts in Step 2 of the wizard.
    /// </summary>
    /// <param name="patientId">UUID of the patient (app.patients.id).</param>
    /// <param name="slotId">UUID of the target slot (app.appointment_slots.id).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Conflict result; <c>hasConflict: false</c> when no overlap is detected.</returns>
    /// <response code="200">Conflict check completed; see <c>hasConflict</c> field.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have Staff or Admin role.</response>
    /// <response code="404">Slot not found.</response>
    [HttpGet("/api/v1/appointments/conflict-check")]
    [ProducesResponseType(typeof(ConflictCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckConflict(
        [FromQuery] Guid patientId,
        [FromQuery] Guid slotId,
        CancellationToken ct)
    {
        try
        {
            var result = await _staffBookingService.CheckConflictAsync(patientId, slotId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

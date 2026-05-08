using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;

/// <summary>
/// Request payload for POST /api/v1/staff-bookings (EP-004 US_035 FR-SO-005).
///
/// AC-1: Staff creates a booking without patient-side verification requirements.
/// AC-3: <see cref="NewPatient"/> enables inline patient profile creation when
///       the patient does not yet have an account; mutually exclusive with <see cref="PatientId"/>.
/// Edge Case 1: If <see cref="OverrideReason"/> is absent and a conflict exists,
///              the service returns HTTP 409 prompting the client to collect a reason.
/// Edge Case 2: Self-booking validation — service ensures staffUserId != patient.UserId.
/// </summary>
public sealed class CreateStaffBookingRequest
{
    /// <summary>
    /// UUID of an existing patient record.
    /// Mutually exclusive with <see cref="NewPatient"/> — exactly one must be provided.
    /// </summary>
    public Guid? PatientId { get; init; }

    /// <summary>
    /// Inline patient creation payload.
    /// Mutually exclusive with <see cref="PatientId"/>.
    /// </summary>
    public InlinePatientPayload? NewPatient { get; init; }

    /// <summary>UUID of the appointment slot returned by GET /api/v1/appointments/slots.</summary>
    [Required]
    public required Guid SlotId { get; init; }

    /// <summary>Free-text visit reason (max 500 chars).</summary>
    [Required]
    [MaxLength(500)]
    [MinLength(1)]
    public required string VisitReason { get; init; }

    /// <summary>
    /// Override reason (max 300 chars) — required when a scheduling conflict was
    /// detected and the staff member acknowledged it in the client flow (Edge Case 1).
    /// If absent and a conflict exists, the API returns HTTP 409.
    /// </summary>
    [MaxLength(300)]
    public string? OverrideReason { get; init; }
}

using PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Staff-assisted booking service abstraction (EP-004 US_035 FR-SO-005).
///
/// AC-1: Creates appointments without patient-side verification.
/// AC-2: All bookings record the acting staff member's identity.
/// AC-3: Supports inline patient creation when the patient has no existing account.
/// AC-4: Every booking writes an immutable audit record.
/// Edge Case 1: Returns HTTP 409 payload when a conflict exists and no override reason is provided.
/// Edge Case 2: Throws <see cref="InvalidOperationException"/> on self-booking attempts.
/// </summary>
public interface IStaffBookingService
{
    /// <summary>
    /// Creates an appointment on behalf of a patient, attributing it to the acting staff member.
    /// </summary>
    /// <param name="request">
    /// Booking payload: patient ID (or inline patient form), slot ID, visit reason,
    /// and optional override reason (required when a conflict was acknowledged).
    /// </param>
    /// <param name="staffUserId">UUID of the authenticated staff member (from JWT <c>sub</c> claim).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Booking details including IDs for the appointment, patient, and staff actor.</returns>
    /// <exception cref="ArgumentException">
    /// Neither <c>PatientId</c> nor <c>NewPatient</c> provided, or both are provided.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Self-booking attempt detected (<c>staffUserId == patientUserId</c>).
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Referenced patient or slot not found.
    /// </exception>
    /// <exception cref="SlotConflictException">
    /// A scheduling conflict exists but no override reason was supplied.
    /// Inspect <see cref="SlotConflictException.ConflictDetails"/> for the conflicting appointment.
    /// </exception>
    Task<StaffBookingResponse> CreateBookingAsync(
        CreateStaffBookingRequest request,
        Guid staffUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks whether the given patient has a confirmed appointment that overlaps
    /// the requested slot. Used by the frontend to detect conflicts before booking.
    /// </summary>
    /// <param name="patientId">UUID of the patient (app.patients.id).</param>
    /// <param name="slotId">UUID of the target slot (app.appointment_slots.id).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Conflict result; <see cref="ConflictCheckResponse.HasConflict"/> is false when clear.</returns>
    /// <exception cref="KeyNotFoundException">Slot not found.</exception>
    Task<ConflictCheckResponse> CheckConflictAsync(
        Guid patientId,
        Guid slotId,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown by <see cref="IStaffBookingService.CreateBookingAsync"/> when a scheduling
/// conflict exists and no override reason was provided, signalling HTTP 409.
/// </summary>
public sealed class SlotConflictException : Exception
{
    public ConflictCheckResponse ConflictDetails { get; }

    public SlotConflictException(ConflictCheckResponse conflictDetails)
        : base("A scheduling conflict exists. Provide an override reason to proceed.")
    {
        ConflictDetails = conflictDetails;
    }
}

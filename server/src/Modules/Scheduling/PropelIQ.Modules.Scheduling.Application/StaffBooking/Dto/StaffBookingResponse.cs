namespace PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;

/// <summary>
/// Response returned by POST /api/v1/staff-bookings on success (EP-004 US_035 AC-2, AC-4).
///
/// AC-2: <see cref="StaffActorId"/> records the staff member who performed the booking
///       so it is attributed in the audit log rather than appearing anonymous.
/// </summary>
public sealed class StaffBookingResponse
{
    /// <summary>UUID of the created booking / audit record (for client-side correlation).</summary>
    public required Guid BookingId { get; init; }

    /// <summary>UUID of the <c>Appointment</c> entity created for this booking.</summary>
    public required Guid AppointmentId { get; init; }

    /// <summary>UUID of the patient for whom the booking was created.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>
    /// Deep link to the booking confirmation page (SCR-006).
    /// Null when the confirmation URL cannot be generated at booking time.
    /// </summary>
    public string? ConfirmationUrl { get; init; }

    /// <summary>UUID of the staff member who created the booking (AC-2 audit attribution).</summary>
    public required Guid StaffActorId { get; init; }
}

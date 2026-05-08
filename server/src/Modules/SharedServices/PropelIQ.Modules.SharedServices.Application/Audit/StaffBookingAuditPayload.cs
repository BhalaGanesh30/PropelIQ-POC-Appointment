namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Structured payload for staff-assisted booking audit events (EP-004 US_035 AC-4, NFR-010).
///
/// Written by <see cref="IAuditService.LogStaffBookingAsync"/> with
/// <c>EventType = "StaffBooking"</c> and on-behalf-of attribution details.
/// </summary>
public sealed class StaffBookingAuditPayload
{
    /// <summary>UUID of the created Appointment entity.</summary>
    public required Guid AppointmentId { get; init; }

    /// <summary>UUID of the patient for whom the booking was created.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>UUID of the slot that was reserved.</summary>
    public required Guid SlotId { get; init; }

    /// <summary>Visit reason provided by staff at booking time.</summary>
    public required string VisitReason { get; init; }

    /// <summary>Override reason if a scheduling conflict was acknowledged; null otherwise.</summary>
    public string? OverrideReason { get; init; }

    /// <summary>
    /// Whether the patient account was created inline during this booking (AC-3).
    /// </summary>
    public bool InlinePatientCreated { get; init; }
}

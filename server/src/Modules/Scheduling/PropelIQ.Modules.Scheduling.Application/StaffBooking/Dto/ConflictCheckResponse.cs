namespace PropelIQ.Modules.Scheduling.Application.StaffBooking.Dto;

/// <summary>
/// Response returned by GET /api/v1/appointments/conflict-check (EP-004 US_035 Edge Case 1).
///
/// When <see cref="HasConflict"/> is true, the client must collect an override reason
/// before re-submitting the booking via POST /api/v1/staff-bookings.
/// </summary>
public sealed class ConflictCheckResponse
{
    /// <summary>Whether the patient has an existing appointment that overlaps the requested slot.</summary>
    public required bool HasConflict { get; init; }

    /// <summary>UUID of the conflicting appointment (populated only when <see cref="HasConflict"/> is true).</summary>
    public Guid? ConflictingAppointmentId { get; init; }

    /// <summary>Scheduled datetime of the conflicting appointment (ISO 8601).</summary>
    public DateTimeOffset? ConflictingDateTime { get; init; }

    /// <summary>
    /// Brief description of the conflicting appointment for display in the conflict banner.
    /// Example: "Cardiology follow-up with Dr. Patel".
    /// </summary>
    public string? ConflictingReason { get; init; }
}

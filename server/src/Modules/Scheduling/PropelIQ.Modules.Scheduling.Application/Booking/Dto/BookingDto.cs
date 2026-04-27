namespace PropelIQ.Modules.Scheduling.Application.Booking.Dto;

/// <summary>POST /api/v1/bookings request body.</summary>
public record CreateBookingRequest
{
    /// <summary>ID of the slot to atomically reserve (AC-1).</summary>
    public Guid SlotId { get; init; }

    /// <summary>
    /// Finalized intake record ID (AC-4).
    /// Optional — may be linked after intake submission completes.
    /// </summary>
    public Guid? IntakeRecordId { get; init; }
}

/// <summary>
/// Successful booking response — returned as HTTP 201 Created.
/// Includes the confirmation code and denormalized appointment details
/// so the frontend can display a confirmation screen without a second request.
/// </summary>
public record BookingResponse
{
    public Guid AppointmentId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTimeOffset AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset BookedAt { get; init; }
}

/// <summary>
/// Returned as HTTP 409 Conflict when optimistic concurrency detects that the
/// requested slot was taken between the availability check and the commit (AC-4).
/// Includes the next available slot so the frontend can immediately offer an alternative.
/// </summary>
public record SlotConflictResponse
{
    public string Message { get; init; } = "Slot no longer available";
    public Guid? NextAvailableSlotId { get; init; }
    public DateTimeOffset? NextAvailableTime { get; init; }
}

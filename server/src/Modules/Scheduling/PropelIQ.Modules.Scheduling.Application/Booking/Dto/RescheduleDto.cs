namespace PropelIQ.Modules.Scheduling.Application.Booking.Dto;

/// <summary>POST /api/v1/bookings/{id}/cancel request body.</summary>
public record CancelBookingRequest
{
    /// <summary>
    /// Required for staff overrides within the 24-hour policy window (AC-4).
    /// Optional for standard patient cancellations.
    /// </summary>
    public string? OverrideReason { get; init; }
}

/// <summary>Successful cancellation response — HTTP 200 OK.</summary>
public record CancelBookingResponse
{
    public Guid AppointmentId { get; init; }
    public string Status { get; init; } = "Cancelled";
    public DateTimeOffset CancelledAt { get; init; }
}

/// <summary>POST /api/v1/bookings/{id}/reschedule request body.</summary>
public record RescheduleBookingRequest
{
    /// <summary>ID of the new slot to atomically reserve (AC-2).</summary>
    public Guid NewSlotId { get; init; }

    /// <summary>
    /// Required for staff overrides within the 24-hour policy window (AC-4).
    /// Optional for standard patient reschedules.
    /// </summary>
    public string? OverrideReason { get; init; }
}

/// <summary>Successful reschedule response — HTTP 200 OK.</summary>
public record RescheduleBookingResponse
{
    public Guid AppointmentId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTimeOffset NewAppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string Status { get; init; } = "Confirmed";
    public DateTimeOffset RescheduledAt { get; init; }
}

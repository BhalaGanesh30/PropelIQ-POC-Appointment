namespace PropelIQ.Modules.Scheduling.Domain.Events;

/// <summary>
/// Raised after an appointment is successfully cancelled.
/// Consumed by background handlers to send cancellation confirmation email (AC-1).
/// Published AFTER the appointment status is persisted — email failure never reverts cancellation.
/// </summary>
public sealed record BookingCancelledEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTimeOffset OriginalAppointmentTime { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public int DurationMinutes { get; init; }
    public string? Location { get; init; }
    public string? PatientEmail { get; init; }
    public DateTimeOffset CancelledAt { get; init; }

    /// <summary>
    /// RFC 5545 SEQUENCE number at cancellation — cancellation ICS uses this + 1 per RFC 5545
    /// so calendar clients process the cancellation as the latest version (US_024 task_001).
    /// </summary>
    public int SequenceNumber { get; init; }
}

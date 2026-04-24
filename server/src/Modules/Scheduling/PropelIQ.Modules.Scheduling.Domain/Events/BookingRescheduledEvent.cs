namespace PropelIQ.Modules.Scheduling.Domain.Events;

/// <summary>
/// Raised after an appointment is successfully rescheduled.
/// Consumed by background handlers to send updated confirmation artifacts (AC-2).
/// Published AFTER the atomic slot swap commits — notification failure never reverts reschedule.
/// </summary>
public sealed record BookingRescheduledEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTimeOffset OriginalTime { get; init; }
    public DateTimeOffset NewTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }
    public string? PatientEmail { get; init; }
    public DateTimeOffset RescheduledAt { get; init; }

    /// <summary>
    /// RFC 5545 SEQUENCE number after the reschedule increment — carried here so the
    /// background event handler does not need a second DB round-trip (US_024 task_001).
    /// </summary>
    public int SequenceNumber { get; init; }
}

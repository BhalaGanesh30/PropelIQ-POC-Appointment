namespace PropelIQ.Modules.Scheduling.Domain.Events;

/// <summary>
/// Raised after an appointment booking is atomically persisted.
/// Consumed by background handlers for:
///   - PDF / QR / ICS confirmation artifact generation (task_002).
///   - Email / SMS notification dispatch (edge case: retried 3× with backoff).
///
/// The event is published AFTER SaveChanges so the booking is guaranteed to be
/// committed — notification failure never rolls back the booking record.
/// </summary>
public sealed record BookingConfirmedEvent
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public string ConfirmationCode { get; init; } = string.Empty;
    public DateTimeOffset AppointmentTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? Location { get; init; }

    /// <summary>
    /// Patient contact details for notification — populated from the Patient
    /// record by the notification handler; not forwarded to AI or external services.
    /// </summary>
    public string? PatientEmail { get; init; }
    public string? PatientPhone { get; init; }
}

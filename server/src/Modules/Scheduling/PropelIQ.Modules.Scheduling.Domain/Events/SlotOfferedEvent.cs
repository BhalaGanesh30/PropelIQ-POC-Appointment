namespace PropelIQ.Modules.Scheduling.Domain.Events;

/// <summary>
/// Published when a matching slot is offered to the first eligible waitlisted patient (AC-2).
/// Consumed by the notification handler to send the preferred-slot alert email/SMS
/// within 5 minutes of the slot becoming available.
/// </summary>
public sealed record SlotOfferedEvent
{
    public Guid WaitlistEntryId { get; init; }
    public Guid PatientId { get; init; }
    public Guid SlotId { get; init; }
    public DateTimeOffset SlotTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }

    /// <summary>UTC deadline by which the patient must claim the slot (AC-4).</summary>
    public DateTimeOffset ClaimExpiresAt { get; init; }

    /// <summary>Patient contact for notification — not forwarded to external services.</summary>
    public string? PatientEmail { get; init; }
}

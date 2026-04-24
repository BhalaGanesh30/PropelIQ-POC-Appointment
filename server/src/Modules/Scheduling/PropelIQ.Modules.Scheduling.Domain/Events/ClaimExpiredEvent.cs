namespace PropelIQ.Modules.Scheduling.Domain.Events;

/// <summary>
/// Published when a waitlist claim window expires without the patient claiming (AC-4).
/// Consumed by the notification handler to inform the patient the offer lapsed
/// and the slot has been offered to the next eligible patient.
/// </summary>
public sealed record ClaimExpiredEvent
{
    public Guid WaitlistEntryId { get; init; }
    public Guid PatientId { get; init; }
    public Guid SlotId { get; init; }

    /// <summary>Patient contact for notification — not forwarded to external services.</summary>
    public string? PatientEmail { get; init; }
}

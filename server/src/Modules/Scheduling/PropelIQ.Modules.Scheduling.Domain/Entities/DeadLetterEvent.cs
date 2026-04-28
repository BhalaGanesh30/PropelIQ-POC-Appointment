using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// AC-4: Captures failed reminder dispatch payloads after all retry attempts
/// are exhausted. Enables manual review, operational alerting, and optional
/// re-processing of delivery failures.
/// </summary>
public sealed class DeadLetterEvent : BaseEntity
{
    /// <summary>ID of the original <see cref="ReminderEvent"/> that failed.</summary>
    public required Guid SourceReminderId { get; init; }

    /// <summary>Appointment associated with the failed reminder.</summary>
    public required Guid AppointmentId { get; init; }

    /// <summary>Notification channel that failed (Email or Sms).</summary>
    public required string Channel { get; init; }

    /// <summary>Root exception message from the final failed delivery attempt.</summary>
    public required string FailureReason { get; init; }

    /// <summary>Total dispatch attempts made before exhaustion.</summary>
    public required int TotalAttempts { get; init; }

    /// <summary>Whether this dead-letter has been manually reprocessed.</summary>
    public bool Reprocessed { get; set; }
}

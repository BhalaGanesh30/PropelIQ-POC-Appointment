using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

public sealed class ReminderEvent : BaseEntity
{
    public required Guid AppointmentId { get; set; }
    public required string Channel { get; set; }
    public string SendStatus { get; set; } = ReminderSendStatus.Pending;
    public string? ConfirmationResponse { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// The UTC time at which this reminder should be dispatched
    /// (appointment start time minus the configured offset).
    /// </summary>
    public required DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// Composite key: {AppointmentId}_{OffsetMinutes}_{Channel}.
    /// Prevents duplicate ReminderEvent rows on retries or duplicate event delivery.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    public Appointment Appointment { get; set; } = null!;
}

/// <summary>
/// String constants for ReminderEvent.SendStatus — avoids magic strings across layers.
/// </summary>
public static class ReminderSendStatus
{
    public const string Pending   = "Pending";
    /// <summary>
    /// Transient status set atomically by the dispatch worker before sending.
    /// Prevents duplicate dispatch across concurrent worker instances (edge case 2).
    /// </summary>
    public const string Sending   = "Sending";
    public const string Sent      = "Sent";
    public const string Cancelled = "Cancelled";
    public const string Failed    = "Failed";
}

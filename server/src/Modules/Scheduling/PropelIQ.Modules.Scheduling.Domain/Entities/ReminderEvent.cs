using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

public sealed class ReminderEvent : BaseEntity
{
    public required Guid AppointmentId { get; set; }
    public required string Channel { get; set; }
    public string SendStatus { get; set; } = "Pending";
    public string? ConfirmationResponse { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    public Appointment Appointment { get; set; } = null!;
}

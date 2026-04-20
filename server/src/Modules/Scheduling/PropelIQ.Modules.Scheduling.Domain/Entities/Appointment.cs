using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

public sealed class Appointment : BaseEntity
{
    public required Guid PatientId { get; set; }
    public required Guid StaffUserId { get; set; }
    public required DateTimeOffset ScheduledAt { get; set; }
    public required int DurationMinutes { get; set; }
    public required string AppointmentType { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string QueueState { get; set; } = "NotQueued";

    public WaitlistEntry? WaitlistEntry { get; set; }
    public ICollection<ReminderEvent> ReminderEvents { get; set; } = [];
}

using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

public sealed class WaitlistEntry : BaseEntity
{
    public required Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public required int Priority { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset? OfferedAt { get; set; }

    public Appointment? Appointment { get; set; }
}

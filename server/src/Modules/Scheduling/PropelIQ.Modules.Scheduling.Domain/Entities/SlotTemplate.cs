using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Defines recurring availability patterns from which concrete AppointmentSlot
/// instances are generated (e.g. Mon–Fri 09:00–17:00 in 30-minute blocks).
/// </summary>
public sealed class SlotTemplate : BaseEntity
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SlotDuration DefaultDuration { get; set; }
    public AppointmentType Type { get; set; }
    public int MaxCapacity { get; set; } = 1;
    public Guid? ProviderId { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

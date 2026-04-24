using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Scheduling.Domain.Entities;

/// <summary>
/// Represents a bookable time slot for an appointment.
/// RowVersion enables optimistic concurrency to handle race conditions
/// when multiple patients attempt to book the same slot simultaneously.
/// </summary>
public sealed class AppointmentSlot : BaseEntity
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public SlotDuration Duration { get; set; }
    public AppointmentType Type { get; set; }
    public int MaxCapacity { get; set; } = 1;
    public int CurrentBookings { get; set; } = 0;
    public Guid? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? Location { get; set; }

    // Computed availability — not persisted
    public bool IsAvailable => CurrentBookings < MaxCapacity
                               && StartTime > DateTimeOffset.UtcNow;

    // Optimistic concurrency token (edge case: booking race condition)
    public uint RowVersion { get; set; }
}

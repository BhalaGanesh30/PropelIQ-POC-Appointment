using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Data access abstraction for appointment slot queries.
/// Implemented in the Infrastructure layer by SlotRepository.
/// </summary>
public interface ISlotRepository
{
    /// <summary>
    /// Returns available (future, not fully booked) slots within the given date range,
    /// optionally filtered by duration and appointment type (AC-2).
    /// </summary>
    Task<List<AppointmentSlot>> SearchAvailableSlotsAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        SlotDuration? duration,
        AppointmentType? type,
        CancellationToken ct);
}

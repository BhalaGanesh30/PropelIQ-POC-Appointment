using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Scheduling;

/// <summary>
/// EF Core implementation of ISlotRepository.
/// Queries AppointmentSlots excluding past and fully-booked slots (AC-2).
/// </summary>
public sealed class SlotRepository : ISlotRepository
{
    private readonly AppDbContext _context;

    public SlotRepository(AppDbContext context) => _context = context;

    public async Task<List<AppointmentSlot>> SearchAvailableSlotsAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        SlotDuration? duration,
        AppointmentType? type,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dateFromUtc = dateFrom.ToUniversalTime();
        var dateToUtc = dateTo.ToUniversalTime();

        var query = _context.AppointmentSlots
            .AsNoTracking()
            .Where(s => s.StartTime >= dateFromUtc
                     && s.StartTime <= dateToUtc
                     && s.StartTime > now              // Future only (AC-2)
                     && s.CurrentBookings < s.MaxCapacity); // Exclude fully-booked (AC-2)

        if (duration.HasValue)
            query = query.Where(s => s.Duration == duration.Value);

        if (type.HasValue)
            query = query.Where(s => s.Type == type.Value);

        return await query
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Appointments.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Appointments;

/// <summary>
/// EF Core implementation of <see cref="IAppointmentHistoryRepository"/>.
///
/// AC-1 / AC-2 / AC-3: Queries apply optional status/date-range predicates,
/// sort by <c>ScheduledAt</c> descending, then skip/take for pagination.
/// The composite index on (PatientId, ScheduledAt DESC, Status) covers all
/// filter combinations and satisfies the 500 ms p95 latency requirement (AC-2).
///
/// AC-4: <see cref="StreamFilteredAsync"/> uses <c>AsNoTracking</c> +
/// <c>AsAsyncEnumerable</c> so the PDF generator can iterate rows incrementally
/// without loading the full result into memory.
/// </summary>
public sealed class AppointmentHistoryRepository : IAppointmentHistoryRepository
{
    private readonly AppDbContext _context;

    public AppointmentHistoryRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<(List<Appointment> Items, int TotalCount)> GetFilteredAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        var query = BuildBaseQuery(patientId, filter);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<Appointment> StreamFilteredAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        // AsAsyncEnumerable streams rows one-by-one to keep memory flat (AC-4).
        return BuildBaseQuery(patientId, filter).AsAsyncEnumerable();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private IQueryable<Appointment> BuildBaseQuery(
        Guid patientId,
        AppointmentHistoryFilter filter)
    {
        var query = _context.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patientId);

        if (filter.Status is not null)
            query = query.Where(a => a.Status == filter.Status);

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.ScheduledAt >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(a => a.ScheduledAt <= filter.DateTo.Value);

        // AC-1: results sorted date descending.
        return query.OrderByDescending(a => a.ScheduledAt);
    }
}

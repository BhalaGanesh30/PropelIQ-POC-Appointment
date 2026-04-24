using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

/// <summary>
/// EF Core implementation of <see cref="IWaitlistRepository"/> (US_023).
///
/// FIFO ordering: entries are queried by <see cref="WaitlistEntry.Position"/>
/// then <see cref="WaitlistEntry.CreatedAt"/> so earlier joiners are offered
/// first (AC-2).
/// </summary>
public sealed class WaitlistRepository : IWaitlistRepository
{
    private readonly AppDbContext _context;

    public WaitlistRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<WaitlistEntry> AddAsync(WaitlistEntry entry, CancellationToken ct)
    {
        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        return entry;
    }

    /// <inheritdoc />
    public async Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == id, ct);

    /// <inheritdoc />
    public async Task<WaitlistEntry?> GetByIdForPatientAsync(
        Guid id, Guid patientId, CancellationToken ct)
        => await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.PatientId == patientId, ct);

    /// <inheritdoc />
    public async Task<List<WaitlistEntry>> GetActiveEntriesForPatientAsync(
        Guid patientId, CancellationToken ct)
        => await _context.WaitlistEntries
            .Where(w => w.PatientId == patientId
                     && (w.Status == WaitlistStatus.Active
                      || w.Status == WaitlistStatus.Offered))
            .OrderBy(w => w.Position)
            .ThenBy(w => w.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<WaitlistEntry>> FindEligibleEntriesForSlotAsync(
        DateTimeOffset slotTime,
        int durationMinutes,
        string appointmentType,
        CancellationToken ct)
        => await _context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Active
                     && w.PreferredDateStart <= slotTime
                     && w.PreferredDateEnd >= slotTime
                     && w.PreferredDurationMinutes == durationMinutes
                     && w.PreferredAppointmentType == appointmentType)
            .OrderBy(w => w.Position)
            .ThenBy(w => w.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<WaitlistEntry>> GetExpiredOffersAsync(CancellationToken ct)
        => await _context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Offered
                     && w.ClaimExpiresAt <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task UpdateAsync(WaitlistEntry entry, CancellationToken ct)
    {
        _context.WaitlistEntries.Update(entry);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> GetNextPositionAsync(CancellationToken ct)
    {
        var max = await _context.WaitlistEntries
            .MaxAsync(w => (int?)w.Position, ct) ?? 0;
        return max + 1;
    }
}

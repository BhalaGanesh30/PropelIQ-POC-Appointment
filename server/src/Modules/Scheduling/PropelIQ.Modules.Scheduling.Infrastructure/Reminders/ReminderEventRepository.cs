using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// EF Core implementation of <see cref="IReminderEventRepository"/>.
/// Uses <see cref="AppDbContext"/> which already exposes <c>DbSet&lt;ReminderEvent&gt;</c>.
/// </summary>
public sealed class ReminderEventRepository : IReminderEventRepository
{
    private readonly AppDbContext _db;

    public ReminderEventRepository(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    /// Idempotent: each event is only inserted if its IdempotencyKey does not already exist.
    public async Task AddRangeAsync(
        IEnumerable<ReminderEvent> events,
        CancellationToken ct = default)
    {
        foreach (var evt in events)
        {
            var exists = await _db.ReminderEvents
                .AnyAsync(r => r.IdempotencyKey == evt.IdempotencyKey, ct);

            if (!exists)
                _db.ReminderEvents.Add(evt);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    /// Single bulk UPDATE — no individual row loads required (AC-3).
    public async Task CancelPendingByAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        await _db.ReminderEvents
            .Where(r =>
                r.AppointmentId == appointmentId &&
                r.SendStatus    == ReminderSendStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.SendStatus, ReminderSendStatus.Cancelled),
                ct);
    }

    /// <inheritdoc/>
    public async Task<ReminderEvent?> GetByIdAsync(
        Guid reminderId,
        CancellationToken ct = default)
    {
        return await _db.ReminderEvents
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct);
    }

    /// <inheritdoc/>
    public async Task RecordConfirmationResponseAsync(
        Guid reminderId,
        string response,
        CancellationToken ct = default)
    {
        await _db.ReminderEvents
            .Where(r => r.Id == reminderId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.ConfirmationResponse, response),
                ct);
    }
}

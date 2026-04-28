using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// EF Core implementation of <see cref="IReminderDispatchRepository"/>.
///
/// AC-2: <see cref="GetDueRemindersAsync"/> applies a 1-minute tolerance window.
/// Edge case 2: <see cref="TryClaimForDispatchAsync"/> uses a conditional
/// <c>ExecuteUpdateAsync</c> for optimistic Pending→Sending transition,
/// preventing duplicate dispatch across concurrent worker instances.
/// Edge case 1: All state is persisted; on worker restart, overdue Pending
/// reminders are immediately eligible on the next <see cref="GetDueRemindersAsync"/> call.
/// </summary>
public sealed class ReminderDispatchRepository : IReminderDispatchRepository
{
    private readonly AppDbContext _db;

    public ReminderDispatchRepository(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReminderEvent>> GetDueRemindersAsync(
        DateTimeOffset now,
        TimeSpan tolerance,
        int batchSize,
        CancellationToken ct = default)
    {
        var cutoff = now + tolerance;

        return await _db.ReminderEvents
            .Where(r =>
                r.SendStatus == ReminderSendStatus.Pending &&
                r.ScheduledAt <= cutoff)
            .OrderBy(r => r.ScheduledAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    /// Conditional UPDATE: only succeeds if the row is still Pending,
    /// preventing a second worker from double-dispatching the same reminder.
    public async Task<bool> TryClaimForDispatchAsync(
        Guid reminderId,
        CancellationToken ct = default)
    {
        var affected = await _db.ReminderEvents
            .Where(r =>
                r.Id == reminderId &&
                r.SendStatus == ReminderSendStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.SendStatus, ReminderSendStatus.Sending),
                ct);

        return affected > 0;
    }

    /// <inheritdoc/>
    public async Task MarkSentAsync(
        Guid reminderId,
        DateTimeOffset sentAt,
        CancellationToken ct = default)
    {
        await _db.ReminderEvents
            .Where(r => r.Id == reminderId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.SendStatus, ReminderSendStatus.Sent)
                    .SetProperty(r => r.SentAt, sentAt),
                ct);
    }

    /// <inheritdoc/>
    public async Task MarkRetryOrFailedAsync(
        Guid reminderId,
        int maxRetries,
        CancellationToken ct = default)
    {
        var reminder = await _db.ReminderEvents
            .FirstAsync(r => r.Id == reminderId, ct);

        reminder.RetryCount++;

        reminder.SendStatus = reminder.RetryCount >= maxRetries
            ? ReminderSendStatus.Failed
            : ReminderSendStatus.Pending;

        await _db.SaveChangesAsync(ct);
    }
}

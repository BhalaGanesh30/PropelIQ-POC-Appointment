using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// Repository abstraction for the dispatch worker's read/update operations
/// on <see cref="ReminderEvent"/> rows.
///
/// AC-2: Querying due reminders with a 1-minute tolerance window.
/// Edge case 2: Optimistic Pending→Sending claim prevents duplicate dispatch.
/// </summary>
public interface IReminderDispatchRepository
{
    /// <summary>
    /// AC-2: Returns up to <paramref name="batchSize"/> reminders where
    /// <c>SendStatus = Pending</c> and <c>ScheduledAt &lt;= now + tolerance</c>,
    /// ordered by <c>ScheduledAt</c> ascending (oldest-due first).
    /// </summary>
    Task<IReadOnlyList<ReminderEvent>> GetDueRemindersAsync(
        DateTimeOffset now,
        TimeSpan tolerance,
        int batchSize,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically transitions <c>SendStatus</c> from <c>Pending</c> to <c>Sending</c>
    /// using a conditional UPDATE. Returns <c>true</c> when the row was claimed;
    /// <c>false</c> when another worker instance already claimed it (edge case 2).
    /// </summary>
    Task<bool> TryClaimForDispatchAsync(
        Guid reminderId,
        CancellationToken ct = default);

    /// <summary>
    /// Marks the reminder as <c>Sent</c> and records the dispatch timestamp.
    /// </summary>
    Task MarkSentAsync(
        Guid reminderId,
        DateTimeOffset sentAt,
        CancellationToken ct = default);

    /// <summary>
    /// Increments <c>RetryCount</c> and resets <c>SendStatus</c> to <c>Pending</c>
    /// so the next poll picks it up again — unless <c>RetryCount &gt;= maxRetries</c>,
    /// in which case transitions to <c>Failed</c>.
    /// </summary>
    Task MarkRetryOrFailedAsync(
        Guid reminderId,
        int maxRetries,
        CancellationToken ct = default);
}

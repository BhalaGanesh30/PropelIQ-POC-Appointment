using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Reminders;

/// <summary>
/// AC-4: Repository abstraction for dead-letter persistence.
/// When a <see cref="ReminderEvent"/> exhausts all retry attempts and
/// transitions to <see cref="ReminderSendStatus.Failed"/>, a
/// <see cref="DeadLetterEvent"/> is persisted for manual review or re-processing.
/// </summary>
public interface IDeadLetterRepository
{
    /// <summary>Persists a dead-letter record for a failed reminder dispatch.</summary>
    Task AddAsync(DeadLetterEvent deadLetter, CancellationToken ct = default);
}

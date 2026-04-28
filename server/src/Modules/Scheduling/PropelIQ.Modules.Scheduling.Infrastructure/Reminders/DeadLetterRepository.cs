using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// AC-4: EF Core implementation of <see cref="IDeadLetterRepository"/>.
/// Persists <see cref="DeadLetterEvent"/> records when reminder dispatch
/// exhausts all retry attempts.
/// </summary>
public sealed class DeadLetterRepository : IDeadLetterRepository
{
    private readonly AppDbContext _db;

    public DeadLetterRepository(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task AddAsync(DeadLetterEvent deadLetter, CancellationToken ct = default)
    {
        _db.DeadLetterEvents.Add(deadLetter);
        await _db.SaveChangesAsync(ct);
    }
}

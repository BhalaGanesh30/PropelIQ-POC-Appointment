using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDeadLetterRepository"/>.
/// Uses the shared <see cref="AppDbContext"/>.
/// </summary>
public sealed class DeadLetterRepository : IDeadLetterRepository
{
    private readonly AppDbContext _db;

    public DeadLetterRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(DeadLetterEntry entry, CancellationToken ct = default)
    {
        _db.OcrDeadLetterQueue.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DeadLetterEntry>> GetAllAsync(CancellationToken ct = default) =>
        await _db.OcrDeadLetterQueue
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
}

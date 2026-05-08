using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICodingDecisionRepository"/>.
/// Queries the shared <c>app.coding_decisions</c> table.
/// </summary>
public sealed class CodingDecisionRepository : ICodingDecisionRepository
{
    private readonly AppDbContext _db;

    public CodingDecisionRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<bool> ExistsForFactAsync(Guid factId, CancellationToken ct = default)
    {
        return await _db.CodingDecisions
            .AnyAsync(cd => cd.FactId == factId, ct);
    }
}

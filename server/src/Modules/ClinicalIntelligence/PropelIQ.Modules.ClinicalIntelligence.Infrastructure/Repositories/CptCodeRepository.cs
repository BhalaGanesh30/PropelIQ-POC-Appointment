using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICptCodeRepository"/>.
/// Queries the <c>app.cpt_codes</c> reference catalog (US_050, task_003).
/// </summary>
internal sealed class CptCodeRepository : ICptCodeRepository
{
    private readonly AppDbContext _db;

    public CptCodeRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken ct = default)
    {
        // MAX returns null when the table is empty.
        return await _db.CptCodes
            .Select(c => (DateTimeOffset?)c.LastUpdatedAt)
            .MaxAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAndActiveAsync(string cptCode, CancellationToken ct = default)
    {
        return await _db.CptCodes
            .AnyAsync(c => c.CptCode == cptCode && !c.IsDeprecated, ct);
    }
}

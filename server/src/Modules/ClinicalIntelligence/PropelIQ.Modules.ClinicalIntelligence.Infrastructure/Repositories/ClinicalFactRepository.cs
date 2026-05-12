using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IClinicalFactRepository"/>.
/// Uses the shared <see cref="AppDbContext"/> whose <c>ClinicalFacts</c> DbSet
/// is already mapped in <c>SharedServices.Infrastructure</c>.
/// </summary>
public sealed class ClinicalFactRepository : IClinicalFactRepository
{
    private readonly AppDbContext _db;

    public ClinicalFactRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AddRangeAsync(IEnumerable<ClinicalFact> facts, CancellationToken ct = default)
    {
        _db.ClinicalFacts.AddRange(facts);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClinicalFact>> GetByDocumentIdAsync(
        Guid documentId,
        CancellationToken ct = default)
    {
        return await _db.ClinicalFacts
            .Where(f => f.DocumentId == documentId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClinicalFact>> GetByPatientIdAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        return await _db.ClinicalFacts
            .Include(f => f.Document)
            .Where(f => f.Document.PatientId == patientId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(List<ClinicalFact> Facts, int Total)> GetByPatientIdGroupedAsync(
        Guid patientId,
        string factType,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        // Base query: patient-scoped, single category, with source document JOIN (AC-2, AC-3).
        var query = _db.ClinicalFacts
            .Include(f => f.Document)
            .Where(f => f.PatientId == patientId && f.FactType == factType)
            .OrderByDescending(f => f.FactDate ?? f.CreatedAt);

        // Count before paging so the FE can calculate total pages / enable virtual scroll.
        var total = await query.CountAsync(ct);

        var facts = await query
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return (facts, total);
    }

    /// <inheritdoc />
    public async Task<ClinicalFact?> GetByIdAsync(Guid factId, CancellationToken ct = default)
    {
        return await _db.ClinicalFacts
            .Include(f => f.Document)
            .FirstOrDefaultAsync(f => f.Id == factId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        ClinicalFact fact,
        int expectedRowVersion,
        CancellationToken ct = default)
    {
        // Atomic optimistic-concurrency update: only succeeds when row_version matches.
        // Returns 1 when the row was updated, 0 when another writer already changed the version.
        var rowsAffected = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE app.clinical_facts
            SET    name         = {fact.Name},
                   value        = {fact.Value},
                   verified     = {fact.Verified},
                   verified_by  = {fact.VerifiedBy},
                   verified_at  = {fact.VerifiedAt},
                   updated_at   = now(),
                   row_version  = row_version + 1
            WHERE  id          = {fact.Id}
              AND  row_version  = {expectedRowVersion}
            """,
            ct);

        if (rowsAffected == 1)
        {
            // Keep the in-memory entity consistent with the DB after the raw SQL update.
            fact.RowVersion = expectedRowVersion + 1;
        }

        return rowsAffected == 1;
    }

    /// <inheritdoc />
    public async Task<List<TimelineEventDto>> GetTimelineFactsAsync(
        Guid patientId,
        string? factType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        // Patient-scoped base query — PatientId is a direct column (no JOIN needed).
        var query = _db.ClinicalFacts.Where(f => f.PatientId == patientId);

        // Apply fact-type filter when the category is not "All" / null (AC-2).
        if (!string.IsNullOrWhiteSpace(factType))
        {
            query = query.Where(f => f.FactType == factType);
        }

        // Apply date range filters server-side on FactDate (falls back to CreatedAt) (AC-3).
        if (from.HasValue)
        {
            query = query.Where(f => (f.FactDate ?? f.CreatedAt) >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(f => (f.FactDate ?? f.CreatedAt) <= to.Value);
        }

        // Project directly in SQL — avoids loading the full entity graph.
        return await query
            .Select(f => new TimelineEventDto
            {
                EventId     = f.Id,
                EventType   = "fact_added",
                Category    = MapFactTypeToCategory(f.FactType),
                Description = f.Name != null ? $"{f.Name}: {f.Value}" : f.Value,
                EventDate   = f.FactDate ?? f.CreatedAt,
                PatientId   = f.PatientId,
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> HasFactsAsync(Guid patientId, CancellationToken ct = default)
        => _db.ClinicalFacts.AnyAsync(f => f.PatientId == patientId, ct);

    /// <summary>
    /// Maps the storage <paramref name="factType"/> string to a human-readable display category.
    /// Unknown types fall back to "Findings" rather than exposing raw DB values.
    /// </summary>
    private static string MapFactTypeToCategory(string factType) => factType.ToLowerInvariant() switch
    {
        "medication" => "Medications",
        "allergy"    => "Allergies",
        "diagnosis"  => "Diagnoses",
        "finding"    => "Findings",
        _            => "Findings",
    };
}


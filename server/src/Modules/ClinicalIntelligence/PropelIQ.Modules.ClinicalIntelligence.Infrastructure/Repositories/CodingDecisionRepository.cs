using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICodingDecisionRepository"/>.
/// Queries and inserts into the shared <c>app.coding_decisions</c> table.
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> InsertPendingAsync(
        IEnumerable<CodingDecision> decisions,
        CancellationToken ct = default)
    {
        var list = decisions.ToList();
        _db.CodingDecisions.AddRange(list);
        await _db.SaveChangesAsync(ct);
        return list.Select(d => d.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<CodingDecision?> GetByIdAsync(Guid decisionId, CancellationToken ct = default)
    {
        return await _db.CodingDecisions
            .FirstOrDefaultAsync(cd => cd.Id == decisionId, ct);
    }

    /// <inheritdoc />
    public async Task<int> UpdateReviewerActionAsync(
        Guid decisionId,
        ReviewerAction action,
        Guid reviewerId,
        string? finalCode,
        string? finalDescription,
        string? originalIcd10Code,
        string? originalCptCode,
        CancellationToken ct = default)
    {
        // Atomic conditional UPDATE: only transitions rows that are still Pending.
        // Returns 0 if the decision was already decided or does not exist → caller maps to HTTP 409.
        return await _db.CodingDecisions
            .Where(cd => cd.Id == decisionId && cd.ReviewerAction == ReviewerAction.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(cd => cd.ReviewerAction,      action)
                .SetProperty(cd => cd.ReviewedByUserId,    reviewerId)
                .SetProperty(cd => cd.DecidedAt,           DateTimeOffset.UtcNow)
                .SetProperty(cd => cd.FinalizedCode,       finalCode)
                .SetProperty(cd => cd.OriginalIcd10Code,   originalIcd10Code)
                .SetProperty(cd => cd.OriginalCptCode,     originalCptCode),
            ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingDecisionDto>> GetPendingByPatientAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        return await _db.CodingDecisions
            .Where(cd => cd.PatientId == patientId && cd.ReviewerAction == ReviewerAction.Pending)
            .OrderBy(cd => cd.CreatedAt)
            .Select(cd => new PendingDecisionDto
            {
                DecisionId = cd.Id,
                IcdCode    = cd.CodeType == "ICD10" ? cd.SuggestedCode : null,
                CptCode    = cd.CptCode,
                PatientId  = cd.PatientId,
                CreatedAt  = cd.CreatedAt,
            })
            .ToListAsync(ct);
    }
}

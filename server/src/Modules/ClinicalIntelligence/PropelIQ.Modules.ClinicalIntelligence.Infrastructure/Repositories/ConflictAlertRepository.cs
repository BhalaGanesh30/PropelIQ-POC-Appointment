using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IConflictAlertRepository"/>.
///
/// Uses the shared <see cref="AppDbContext"/> per the project's single-context pattern.
/// All writes use optimistic concurrency via EF Core's change tracker.
/// </summary>
public sealed class ConflictAlertRepository : IConflictAlertRepository
{
    private readonly AppDbContext _db;

    public ConflictAlertRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConflictAlert>> GetByPatientIdAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        return await _db.ConflictAlerts
            .Where(a => a.PatientId == patientId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<ConflictAlert?> GetByIdAsync(Guid conflictId, CancellationToken ct = default)
        => _db.ConflictAlerts.FirstOrDefaultAsync(a => a.Id == conflictId, ct);

    /// <inheritdoc />
    public async Task<ConflictAlert> UpsertAsync(
        ConflictAlert alert,
        CancellationToken ct = default)
    {
        // Idempotent insert: if a row already exists for (PatientId, FactIdA, FactIdB),
        // return the existing row without modification so re-evaluations are safe.
        var existing = await _db.ConflictAlerts
            .FirstOrDefaultAsync(
                a => a.PatientId == alert.PatientId
                  && a.FactIdA   == alert.FactIdA
                  && a.FactIdB   == alert.FactIdB,
                ct);

        if (existing is not null)
        {
            return existing;
        }

        _db.ConflictAlerts.Add(alert);
        await _db.SaveChangesAsync(ct);
        return alert;
    }

    /// <inheritdoc />
    public async Task<bool> AcknowledgeAsync(
        Guid conflictId,
        Guid clinicianId,
        DateTimeOffset acknowledgedAt,
        CancellationToken ct = default)
    {
        var alert = await _db.ConflictAlerts
            .FirstOrDefaultAsync(a => a.Id == conflictId, ct);

        if (alert is null)
        {
            return false;
        }

        alert.Acknowledged   = true;
        alert.AcknowledgedBy = clinicianId;
        alert.AcknowledgedAt = acknowledgedAt;

        await _db.SaveChangesAsync(ct);
        return true;
    }
}

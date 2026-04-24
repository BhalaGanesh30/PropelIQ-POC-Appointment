using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Intake;

/// <summary>
/// EF Core implementation of IIntakeDraftRepository.
/// All queries are scoped to the authenticated patient ID for security (NFR-010).
/// </summary>
public sealed class IntakeDraftRepository : IIntakeDraftRepository
{
    private readonly AppDbContext _context;

    public IntakeDraftRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IntakeDraft?> GetByPatientAndSlotAsync(
        Guid patientId,
        Guid? slotId,
        CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .Where(d => d.PatientId == patientId
                     && d.SlotId == slotId
                     && d.Status == IntakeStatus.Draft
                     && d.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IntakeDraft?> GetLatestByPatientAsync(
        Guid patientId,
        CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .Where(d => d.PatientId == patientId
                     && d.Status == IntakeStatus.Draft
                     && d.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IntakeDraft?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.IntakeDrafts
            .FirstOrDefaultAsync(
                d => d.Id == id && d.Status == IntakeStatus.Draft, ct);
    }

    /// <inheritdoc />
    public async Task<IntakeDraft> UpsertAsync(IntakeDraft draft, CancellationToken ct)
    {
        // Check for an existing active draft for this patient+slot combination
        var existing = await _context.IntakeDrafts
            .FirstOrDefaultAsync(
                d => d.PatientId == draft.PatientId
                  && d.SlotId == draft.SlotId
                  && d.Status == IntakeStatus.Draft,
                ct);

        if (existing is not null)
        {
            // Update the existing draft in-place
            existing.Update(draft.FormData, draft.AiPopulatedFields);
        }
        else
        {
            _context.IntakeDrafts.Add(draft);
        }

        await _context.SaveChangesAsync(ct);
        return existing ?? draft;
    }

    /// <inheritdoc />
    public async Task<int> ExpireOldDraftsAsync(CancellationToken ct)
    {
        // Bulk-update expired drafts without loading entities into memory
        return await _context.IntakeDrafts
            .Where(d => d.ExpiresAt <= DateTimeOffset.UtcNow
                     && d.Status == IntakeStatus.Draft)
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.Status, IntakeStatus.Expired),
                ct);
    }
}

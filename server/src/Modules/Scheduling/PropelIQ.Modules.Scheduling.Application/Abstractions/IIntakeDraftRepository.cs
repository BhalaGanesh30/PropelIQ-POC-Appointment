using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Data access abstraction for intake draft persistence.
/// Implemented in the Infrastructure layer by IntakeDraftRepository.
/// All queries are scoped to the authenticated patient ID.
/// </summary>
public interface IIntakeDraftRepository
{
    /// <summary>
    /// Returns the most recent active draft for the patient and slot combination (AC-3).
    /// Returns null when no matching draft exists.
    /// </summary>
    Task<IntakeDraft?> GetByPatientAndSlotAsync(
        Guid patientId,
        Guid? slotId,
        CancellationToken ct);

    /// <summary>
    /// Returns the patient's most recent unsubmitted draft regardless of slot (AC-3).
    /// Used when no slotId filter is specified.
    /// </summary>
    Task<IntakeDraft?> GetLatestByPatientAsync(
        Guid patientId,
        CancellationToken ct);

    /// <summary>
    /// Returns a specific draft by ID, or null if not found / already submitted.
    /// </summary>
    Task<IntakeDraft?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Upserts a draft: updates the existing patient+slot draft if one exists, otherwise inserts.
    /// Returns the persisted entity with updated timestamps.
    /// </summary>
    Task<IntakeDraft> UpsertAsync(IntakeDraft draft, CancellationToken ct);

    /// <summary>
    /// Marks all drafts past their ExpiresAt as Expired.
    /// Called by the background cleanup service (edge case: 7-day retention).
    /// Returns the count of drafts expired.
    /// </summary>
    Task<int> ExpireOldDraftsAsync(CancellationToken ct);
}

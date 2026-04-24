using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Application.Abstractions;

/// <summary>
/// Data access abstraction for waitlist operations (US_023).
/// Implemented in Infrastructure by WaitlistRepository (EF Core).
/// </summary>
public interface IWaitlistRepository
{
    /// <summary>Persists a new waitlist entry and returns it with the generated ID.</summary>
    Task<WaitlistEntry> AddAsync(WaitlistEntry entry, CancellationToken ct);

    /// <summary>Returns a waitlist entry by ID (no patient scope — used by workers).</summary>
    Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Returns a tracked waitlist entry by ID scoped to the given patient.
    /// Returns <see langword="null"/> when the entry does not exist or belongs
    /// to a different patient — enforces ownership at the repository level.
    /// </summary>
    Task<WaitlistEntry?> GetByIdForPatientAsync(Guid id, Guid patientId, CancellationToken ct);

    /// <summary>Returns Active and Offered entries for a patient (ordered by position).</summary>
    Task<List<WaitlistEntry>> GetActiveEntriesForPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>
    /// Returns Active entries whose preferred window and criteria match the given slot
    /// parameters, ordered by Position then CreatedAt (FIFO — AC-2).
    /// </summary>
    Task<List<WaitlistEntry>> FindEligibleEntriesForSlotAsync(
        DateTimeOffset slotTime,
        int durationMinutes,
        string appointmentType,
        CancellationToken ct);

    /// <summary>
    /// Returns Offered entries whose claim window has expired (ClaimExpiresAt &lt;= now — AC-4).
    /// </summary>
    Task<List<WaitlistEntry>> GetExpiredOffersAsync(CancellationToken ct);

    /// <summary>Persists mutations to an existing waitlist entry.</summary>
    Task UpdateAsync(WaitlistEntry entry, CancellationToken ct);

    /// <summary>
    /// Returns the next monotonically-increasing position value for FIFO ordering.
    /// </summary>
    Task<int> GetNextPositionAsync(CancellationToken ct);
}

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// Persistence abstraction for active-session lifecycle management:
/// lookup by user, lookup by token, creation, update, and expired-session queries.
/// </summary>
public interface IActiveSessionRepository
{
    /// <summary>Returns the single active (IsActive = true) session for the given user, or null.</summary>
    Task<ActiveSession?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns the active session matching the opaque session token, or null.</summary>
    Task<ActiveSession?> GetBySessionTokenAsync(string sessionToken, CancellationToken ct = default);

    Task AddAsync(ActiveSession session, CancellationToken ct = default);

    Task UpdateAsync(ActiveSession session, CancellationToken ct = default);

    /// <summary>
    /// Returns all active sessions whose LastActivityAt is on or before <paramref name="cutoff"/>.
    /// Used by <c>SessionCleanupService</c> to expire idle sessions.
    /// </summary>
    Task<List<ActiveSession>> GetExpiredSessionsAsync(DateTime cutoff, CancellationToken ct = default);
}

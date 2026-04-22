using PropelIQ.Modules.SharedServices.Infrastructure.Identity;

namespace PropelIQ.Api.Sessions;

/// <summary>
/// Manages the server-side session lifecycle: creation (with single-session enforcement),
/// extension, validation, and invalidation.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new active session for <paramref name="userId"/>.
    /// If an existing active session is found it is terminated first and the displaced
    /// device is notified via SignalR (AC-3 — single-session enforcement).
    /// </summary>
    Task<ActiveSession> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    /// <summary>
    /// Resets LastActivityAt and ExpiresAt to 15 minutes from now (AC-4).
    /// Throws <see cref="InvalidOperationException"/> when the session is not found or has already expired.
    /// </summary>
    Task ExtendSessionAsync(string sessionToken, CancellationToken ct = default);

    /// <summary>
    /// Marks the user's active session inactive and revokes all refresh tokens (AC-2, AC-3).
    /// </summary>
    Task InvalidateSessionAsync(Guid userId, string reason, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> if the session token belongs to an active, non-expired session.</summary>
    Task<bool> IsSessionValidAsync(string sessionToken, CancellationToken ct = default);
}

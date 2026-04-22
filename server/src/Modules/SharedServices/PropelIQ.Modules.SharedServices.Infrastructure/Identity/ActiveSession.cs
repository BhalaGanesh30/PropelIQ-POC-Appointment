namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// Tracks an authenticated user's active server-side session.
/// A user may have at most one IsActive session at any time (single-session enforcement, AC-3).
/// Last-activity timestamp drives the 15-minute inactivity timeout.
/// </summary>
public sealed class ActiveSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Cryptographically random opaque token returned to the client on login.</summary>
    public string SessionToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Updated on every session/extend call; drives inactivity timeout.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    /// <summary>IPv4 or IPv6 address of the originating login request (max 45 chars).</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent header from the originating login request.</summary>
    public string? UserAgent { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Human-readable reason the session was ended.
    /// Examples: "UserLogout", "NewLoginFromAnotherDevice", "InactivityTimeout".
    /// </summary>
    public string? TerminationReason { get; set; }

    public DateTime? TerminatedAt { get; set; }
}

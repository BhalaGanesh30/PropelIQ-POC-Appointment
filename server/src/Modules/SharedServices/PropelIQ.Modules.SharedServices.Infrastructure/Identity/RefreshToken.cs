namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// Opaque refresh token persisted in the <c>auth</c> schema.
/// Supports one-time-use rotation: each use revokes the old token
/// and issues a new one tracked via <see cref="ReplacedByToken"/>.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }

    /// <summary>Token string that replaced this one during rotation.</summary>
    public string? ReplacedByToken { get; set; }
    public string? RevokeReason { get; set; }

    // ── Computed state ───────────────────────────────────────────────────────
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // ── Navigation ───────────────────────────────────────────────────────────
    public ApplicationUser User { get; set; } = null!;
}

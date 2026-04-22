namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// Persistence abstraction for refresh-token lifecycle management:
/// creation, lookup, rotation, and bulk revocation on suspicious reuse.
/// </summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task UpdateAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>
    /// Revokes every active refresh token for the given user.
    /// Used when a previously-revoked token is reused (edge-case security response).
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);
}

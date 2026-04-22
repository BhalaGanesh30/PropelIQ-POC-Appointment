using System.Security.Claims;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// Abstracts JWT access-token generation, refresh-token creation,
/// and expired-token principal extraction for the auth flow.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a short-lived JWT access token with sub, email, role,
    /// jti claims and 15-minute expiry (NFR-008).
    /// </summary>
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);

    /// <summary>
    /// Creates a cryptographically random 64-byte opaque refresh token
    /// for the given user, tracking the originating IP for anomaly logging.
    /// </summary>
    RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress);

    /// <summary>
    /// Validates an expired access token (skipping lifetime check) and
    /// returns the contained <see cref="ClaimsPrincipal"/>.
    /// Returns <c>null</c> if the token is otherwise invalid.
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

namespace PropelIQ.SharedKernel.Auth;

/// <summary>
/// Strongly-typed options for JWT token generation and validation.
/// Bound from the "Jwt" configuration section; validated at startup.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signing key. Minimum 256 bits (32 chars).
    /// Managed by Vault in production (NFR-008).
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>15 minutes per NFR-008 session timeout requirement.</summary>
    public int AccessTokenExpirationMinutes { get; init; } = 15;

    /// <summary>Sliding window for refresh token validity.</summary>
    public int RefreshTokenExpirationDays { get; init; } = 7;
}

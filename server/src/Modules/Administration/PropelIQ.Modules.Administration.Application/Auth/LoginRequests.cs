namespace PropelIQ.Modules.Administration.Application.Auth;

/// <summary>Login credentials submitted by the client.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Returned on successful login or token refresh.</summary>
public sealed record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Access token lifetime in seconds (900 = 15 min per NFR-008).</summary>
    public int ExpiresIn { get; init; } = 900;

    /// <summary>
    /// Role-appropriate dashboard path for client-side redirect (AC-1).
    /// Absent on token-refresh responses.
    /// </summary>
    public string? RedirectUrl { get; init; }

    /// <summary>
    /// Opaque server-side session token used for session/extend calls (us_017).
    /// Absent on token-refresh responses.
    /// </summary>
    public string? SessionToken { get; init; }
}

/// <summary>Access + refresh token pair submitted to the refresh endpoint.</summary>
public record RefreshRequest(string AccessToken, string RefreshToken);

/// <summary>Refresh token submitted to the logout endpoint for server-side revocation.</summary>
public record LogoutRequest(string RefreshToken);

// ── Session management DTOs (us_017) ─────────────────────────────────────────

/// <summary>Opaque session token submitted to the session/extend endpoint.</summary>
public record ExtendSessionRequest(string SessionToken);

/// <summary>Returned by the session/extend endpoint confirming the new expiry (AC-4).</summary>
public sealed record ExtendSessionResponse
{
    public int ExpiresInSeconds { get; init; }
    public string Message { get; init; } = string.Empty;
}

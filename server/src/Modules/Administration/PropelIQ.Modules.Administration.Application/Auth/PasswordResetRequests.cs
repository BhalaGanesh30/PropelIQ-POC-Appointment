namespace PropelIQ.Modules.Administration.Application.Auth;

// ── Forgot-password DTOs (us_018 AC-1) ───────────────────────────────────────

/// <summary>Email address submitted to the forgot-password endpoint.</summary>
public record ForgotPasswordRequest(string Email);

/// <summary>
/// Returned for both registered and unregistered emails to prevent user enumeration (AC-1).
/// </summary>
public sealed record ForgotPasswordResponse
{
    public string Message { get; init; } =
        "If an account with that email exists, a password reset link has been sent.";
}

// ── Reset-password DTOs (us_018 AC-2) ────────────────────────────────────────

/// <summary>Payload submitted to the reset-password endpoint.</summary>
public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);

/// <summary>Returned on a successful password reset (AC-2).</summary>
public sealed record ResetPasswordResponse
{
    public string Message { get; init; } =
        "Password has been reset successfully. Please log in with your new password.";
}

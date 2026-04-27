using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using PropelIQ.Api.Sessions;
using PropelIQ.Modules.Administration.Application.Auth;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;
using PropelIQ.SharedKernel.Notifications;
namespace PropelIQ.Api.Controllers;

/// <summary>
/// Handles registration, verification, login, token refresh, and logout.
/// Most endpoints are anonymous; logout requires a valid Bearer token.
/// Rate-limited to prevent brute-force and enumeration attacks (OWASP A07).
/// </summary>
public sealed class AuthController : BaseApiController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly INotificationSender _notifications;
    private readonly IDistributedCache _cache;
    private readonly AppDbContext _db;
    private readonly ISessionService _sessionService;
    private readonly AccountLockoutHandler _lockoutHandler;
    private readonly string _clientBaseUrl;
    private readonly string _apiBaseUrl;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokens,
        INotificationSender notifications,
        IDistributedCache cache,
        AppDbContext db,
        ISessionService sessionService,
        AccountLockoutHandler lockoutHandler,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokens = refreshTokens;
        _notifications = notifications;
        _cache = cache;
        _db = db;
        _sessionService = sessionService;
        _lockoutHandler = lockoutHandler;
        _clientBaseUrl = configuration.GetValue<string>("App:ClientBaseUrl")
            ?? "http://localhost:4200";
        _apiBaseUrl = configuration.GetValue<string>("App:ApiBaseUrl")
            ?? "http://localhost:5000";
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account.
    /// On success the caller receives 202 Accepted; a confirmation email is dispatched.
    /// Duplicate emails return 409 Conflict.
    /// Invalid payloads return 422 (handled automatically by FluentValidation + [ApiController]).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("register-policy")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        // Prevent user enumeration: use constant-time duplicate guard.
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Problem(
                title: "Email already registered.",
                statusCode: StatusCodes.Status409Conflict,
                detail: "The supplied email address is already associated with an account.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        // Generate email-confirmation token and send the link.
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var callbackUrl =
            $"{_apiBaseUrl}/api/v1/auth/confirm-email" +
            $"?userId={user.Id}&token={encodedToken}";

        // ── DEV SHORTCUT ────────────────────────────────────────────────────
        // In development, log the confirmation URL prominently so developers
        // can confirm accounts without a real email provider.
        _logger.LogWarning(
            "\n======================================================" +
            "\n[DEV] Email confirmation URL for {Email}:" +
            "\n{Url}" +
            "\n======================================================",
            user.Email, callbackUrl);

        try
        {
            await _notifications.SendEmailAsync(
                request.Email,
                "Confirm your PropelIQ account",
                $"<p>Welcome to PropelIQ!</p><p>Please confirm your account by <a href='{callbackUrl}'>clicking here</a>.</p>",
                ct);
        }
        catch (Exception ex)
        {
            // Email delivery failure must never fail the registration response.
            // The confirmation URL is already logged above and included in dev response.
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", request.Email);
        }

        await WriteAuditAsync(
            eventType: "user.registered",
            actorUserId: user.Id,
            targetEntityId: user.Id,
            targetEntityType: nameof(ApplicationUser),
            description: $"New user registered: {user.Email}",
            ct);

        // Include the confirmation URL in the response body in non-production
        // environments so Swagger / browser callers can confirm without email.
        var isDev = HttpContext.RequestServices
            .GetRequiredService<IHostEnvironment>().IsDevelopment();

        return Accepted(isDev
            ? new { message = "Registration successful. Confirm your account using the URL below (dev only).", confirmationUrl = callbackUrl }
            : (object)new { message = "Registration successful. Please check your email to confirm your account." });
    }

    /// <summary>
    /// Confirm an email address using the token issued during registration.
    /// Called from the link in the confirmation email.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] Guid userId,
        [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Problem(
                title: "User not found.",
                statusCode: StatusCodes.Status400BadRequest);

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return Problem(
                title: "Email confirmation failed.",
                statusCode: StatusCodes.Status400BadRequest,
                detail: string.Join(" ", result.Errors.Select(e => e.Description)));

        await WriteAuditAsync(
            eventType: "user.email_confirmed",
            actorUserId: user.Id,
            targetEntityId: user.Id,
            targetEntityType: nameof(ApplicationUser),
            description: $"Email confirmed: {user.Email}",
            ct: default);

        return Ok(new { message = "Email confirmed successfully." });
    }

    /// <summary>
    /// Generate a 6-digit OTP and deliver it to the user.
    /// Primary channel: email (works with any configured SMTP provider).
    /// Secondary channel: SMS (when a phone number is present and an SMS provider is wired up).
    /// The OTP is stored in Redis with a 10-minute TTL.
    /// In development the OTP is also returned in the response body.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("send-otp")]
    [EnableRateLimiting("otp-policy")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendOtp(
        [FromBody] SendOtpRequest request,
        CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        // Return 404 without leaking whether the email is registered.
        if (user is null)
            return Problem(
                title: "User not found.",
                statusCode: StatusCodes.Status404NotFound);

        var otp = GenerateOtp();
        var cacheKey = $"propeliq:otp:{user.Id}";

        await _cache.SetStringAsync(
            cacheKey,
            otp,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            },
            ct);

        // Always attempt email delivery first — works with any SMTP provider
        // (real or stub). The stub logs the OTP to the console prominently.
        try
        {
            await _notifications.SendEmailAsync(
                user.Email!,
                "Your PropelIQ verification code",
                $"<p>Your PropelIQ verification code is: <strong>{otp}</strong></p>" +
                $"<p>This code expires in <strong>10 minutes</strong>. Do not share it with anyone.</p>",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", user.Email);
        }

        // Also attempt SMS delivery when a phone number is registered.
        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            try
            {
                await _notifications.SendSmsAsync(
                    user.PhoneNumber,
                    $"Your PropelIQ verification code is: {otp}. It expires in 10 minutes.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP SMS to {Phone}", user.PhoneNumber);
            }
        }

        _logger.LogWarning(
            "\n======================================================" +
            "\n[DEV] OTP for {Email}: {Otp}" +
            "\n======================================================",
            user.Email, otp);

        var isDev = HttpContext.RequestServices
            .GetRequiredService<IHostEnvironment>().IsDevelopment();

        return Accepted(isDev
            ? new { message = "OTP sent to your email address.", otp }
            : (object)new { message = "OTP sent to your email address." });
    }

    /// <summary>
    /// Verify the OTP submitted by the user against the Redis-stored value.
    /// On success the phone number is marked as confirmed and the OTP is consumed.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("verify-otp")]
    [EnableRateLimiting("otp-policy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Problem(
                title: "Invalid request.",
                statusCode: StatusCodes.Status400BadRequest);

        var cacheKey = $"propeliq:otp:{user.Id}";
        var storedOtp = await _cache.GetStringAsync(cacheKey, ct);

        // Constant-time comparison to prevent timing attacks.
        if (storedOtp is null || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedOtp),
                System.Text.Encoding.UTF8.GetBytes(request.Otp)))
        {
            return Problem(
                title: "Invalid or expired OTP.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Consume the OTP (single-use).
        await _cache.RemoveAsync(cacheKey, ct);

        user.PhoneNumberConfirmed = true;
        await _userManager.UpdateAsync(user);

        await WriteAuditAsync(
            eventType: "user.otp_verified",
            actorUserId: user.Id,
            targetEntityId: user.Id,
            targetEntityType: nameof(ApplicationUser),
            description: $"Phone OTP verified: {user.Email}",
            ct);

        return Ok(new { message = "OTP verified successfully." });
    }

    /// <summary>
    /// Validate credentials and issue a JWT access token + opaque refresh token.
    /// Returns a role-appropriate dashboard redirect URL for the client (AC-1).
    /// A generic 401 is returned for any credential failure to prevent enumeration (AC-3).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login-policy")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Look up by email — do NOT distinguish missing account from wrong password (AC-3).
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await WriteAuditAsync(
                eventType: "auth.login_failed",
                actorUserId: Guid.Empty,
                targetEntityId: null,
                targetEntityType: nameof(ApplicationUser),
                description: $"Login failed — account not found for: {request.Email}",
                ct);

            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid username or password",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        // DEV SHORTCUT: auto-confirm email so local testing is not blocked by the
        // email confirmation step when no real mail provider is configured.
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment() && !user.EmailConfirmed)
        {
            var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _userManager.ConfirmEmailAsync(user, confirmToken);
            _logger.LogWarning(
                "[DEV] Auto-confirmed email for {Email} to allow login without real email provider.",
                user.Email);
        }

        // CheckPasswordSignInAsync increments lockout counter and respects RequireConfirmedEmail.
        var result = await _signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            // Email not confirmed (production path — dev auto-confirms above).
            if (result.IsNotAllowed)
            {
                await WriteAuditAsync(
                    eventType: "auth.login_failed",
                    actorUserId: user.Id,
                    targetEntityId: user.Id,
                    targetEntityType: nameof(ApplicationUser),
                    description: "Login failed — email not confirmed.",
                    ct);

                return Unauthorized(new ProblemDetails
                {
                    Title = "Email not confirmed",
                    Detail = "Please confirm your email address before logging in. Check your inbox for the confirmation link.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var detail = result.IsLockedOut
                ? "Account locked out after too many failed attempts"
                : "Invalid credentials";

            await WriteAuditAsync(
                eventType: "auth.login_failed",
                actorUserId: user.Id,
                targetEntityId: user.Id,
                targetEntityType: nameof(ApplicationUser),
                description: detail,
                ct);

            if (result.IsLockedOut)
            {
                // Identity resets AccessFailedCount to 0 the moment a lockout is
                // triggered, so a count of 0 here signals a fresh lockout event (AC-3).
                var failedCount = await _userManager.GetAccessFailedCountAsync(user);
                if (failedCount == 0)
                {
                    await _lockoutHandler.HandleAsync(user, ipAddress, ct);
                }

                return Unauthorized(new ProblemDetails
                {
                    Title = "Account temporarily locked",
                    Detail = "Too many failed attempts. Please try again in 30 minutes.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid username or password",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenService.GenerateRefreshToken(user.Id, ipAddress);

        await _refreshTokens.AddAsync(refreshToken, ct);

        // Create active session and enforce single-session constraint (AC-3).
        var session = await _sessionService.CreateSessionAsync(
            user.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            ct);

        await WriteAuditAsync(
            eventType: "auth.login_succeeded",
            actorUserId: user.Id,
            targetEntityId: user.Id,
            targetEntityType: nameof(ApplicationUser),
            description: $"Login succeeded. Roles: {string.Join(", ", roles)}",
            ct);

        // Role-appropriate dashboard redirect (AC-1).
        var redirectUrl = roles.FirstOrDefault() switch
        {
            "Admin" => "/admin/users",
            "Staff" => "/dashboard",
            "Clinician" => "/dashboard",
            _ => "/dashboard"
        };

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresIn = 900,
            RedirectUrl = redirectUrl,
            SessionToken = session.SessionToken,
        });
    }

    /// <summary>
    /// Exchange an expired access token + valid refresh token for a rotated pair (AC-2).
    /// Reuse of a previously revoked refresh token triggers bulk revocation and audit (edge case).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid access token",
                Status = StatusCodes.Status401Unauthorized
            });

        if (!Guid.TryParse(
                principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid token claims",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var existing = await _refreshTokens.GetByTokenAsync(request.RefreshToken, ct);

        if (existing is null || existing.UserId != userId)
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid refresh token",
                Status = StatusCodes.Status401Unauthorized
            });

        // Edge case: revoked token reuse — revoke all tokens and flag suspicious activity.
        if (existing.IsRevoked)
        {
            await _refreshTokens.RevokeAllForUserAsync(
                userId, "Revoked token reuse detected", ct);

            await WriteAuditAsync(
                eventType: "auth.suspicious_token_reuse",
                actorUserId: userId,
                targetEntityId: userId,
                targetEntityType: nameof(ApplicationUser),
                description: "Revoked refresh token reused — all tokens revoked",
                ct);

            return Unauthorized(new ProblemDetails
            {
                Title = "Token has been revoked",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (existing.IsExpired)
            return Unauthorized(new ProblemDetails
            {
                Title = "Refresh token expired",
                Status = StatusCodes.Status401Unauthorized
            });

        // Rotate: revoke the old token and issue a new one.
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken(userId, ipAddress);

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ipAddress;
        existing.ReplacedByToken = newRefreshToken.Token;
        existing.RevokeReason = "Rotated";

        await _refreshTokens.UpdateAsync(existing, ct);
        await _refreshTokens.AddAsync(newRefreshToken, ct);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var roles = await _userManager.GetRolesAsync(user!);
        var accessToken = _jwtTokenService.GenerateAccessToken(user!, roles);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresIn = 900
        });
    }

    /// <summary>
    /// Revoke the caller's refresh token server-side and record the logout event (AC-4).
    /// Requires a valid Bearer access token.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out var userId))
        {
            return Unauthorized();
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var token = await _refreshTokens.GetByTokenAsync(request.RefreshToken, ct);

        if (token is not null && token.UserId == userId && token.IsActive)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.RevokeReason = "Logout";
            await _refreshTokens.UpdateAsync(token, ct);
        }

        // Invalidate the server-side active session and revoke all refresh tokens (AC-2).
        await _sessionService.InvalidateSessionAsync(userId, "UserLogout", ct);

        await WriteAuditAsync(
            eventType: "auth.logout",
            actorUserId: userId,
            targetEntityId: userId,
            targetEntityType: nameof(ApplicationUser),
            description: "User logged out — session and refresh tokens revoked",
            ct);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Initiate password reset — generates a time-limited token and emails the reset link (AC-1).
    /// Returns an identical 200 OK response for both registered and unregistered emails to
    /// prevent user enumeration (OWASP A07 / AC-1).
    /// Rate-limited to 3 requests per 15 minutes per IP (edge case).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-reset-policy")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        // Always return the same response — never reveal whether the email is registered.
        var successResponse = new ForgotPasswordResponse();

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            // Record attempted reset for a non-existent account but return success.
            await WriteAuditAsync(
                eventType: "auth.forgot_password_attempt",
                actorUserId: Guid.Empty,
                targetEntityId: null,
                targetEntityType: nameof(ApplicationUser),
                description: $"Password reset requested for unregistered email: {request.Email}",
                ct);

            return Ok(successResponse);
        }

        // GeneratePasswordResetTokenAsync uses the named "PasswordReset" provider
        // configured with a 24-hour TTL (edge case — us_018).
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var resetLink =
            $"{_clientBaseUrl}/reset-password" +
            $"?email={Uri.EscapeDataString(user.Email!)}&token={encodedToken}";

        // Log the reset link in development so engineers can test without a real SMTP provider.
        _logger.LogWarning(
            "\n======================================================" +
            "\n[DEV] Password reset URL for {Email}:" +
            "\n{Url}" +
            "\n======================================================",
            user.Email, resetLink);

        try
        {
            await _notifications.SendEmailAsync(
                user.Email!,
                "Reset Your PropelIQ Password",
                $"You requested a password reset for your PropelIQ account.\n\n" +
                $"Click the link below to set a new password (expires in 24 hours):\n\n" +
                $"{resetLink}\n\n" +
                $"If you did not request this, you can safely ignore this email.",
                ct);
        }
        catch (Exception ex)
        {
            // Email failure must not leak whether the account exists; log and continue.
            _logger.LogError(ex, "Failed to send password-reset email to {Email}", user.Email);
        }

        await WriteAuditAsync(
            eventType: "auth.forgot_password_sent",
            actorUserId: user.Id,
            targetEntityId: user.Id,
            targetEntityType: nameof(ApplicationUser),
            description: "Password reset email dispatched",
            ct);

        return Ok(successResponse);
    }

    /// <summary>
    /// Complete password reset — validates the token, applies the new password, and
    /// invalidates all active sessions and refresh tokens (AC-2).
    /// Returns a generic 400 for invalid/expired tokens to prevent token enumeration.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        // Generic error used for all failure paths to prevent enumeration.
        static IActionResult InvalidOrExpired(ControllerBase ctrl) =>
            ctrl.Problem(
                title: "Password reset failed",
                detail: "The reset link is invalid or has expired.",
                statusCode: StatusCodes.Status400BadRequest);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return InvalidOrExpired(this);

        // Tokens are URL-escaped by the ForgotPassword endpoint — decode before use.
        var decodedToken = Uri.UnescapeDataString(request.Token);
        var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);

        if (!result.Succeeded)
        {
            // Covers: expired token (>24 h), already-used token, tampered token.
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));

            await WriteAuditAsync(
                eventType: "auth.reset_password_failed",
                actorUserId: user.Id,
                targetEntityId: user.Id,
                targetEntityType: nameof(ApplicationUser),
                description: $"Password reset failed: {errors}",
                ct);

            return InvalidOrExpired(this);
        }

        // AC-2: invalidate all active sessions and revoke all refresh tokens so
        // compromised credentials cannot be reused after a successful reset.
        await _sessionService.InvalidateSessionAsync(user.Id, "PasswordReset", ct);
        await _refreshTokens.RevokeAllForUserAsync(
            user.Id, "Password reset — all sessions revoked", ct);

        await WriteAuditAsync(
            eventType: "auth.reset_password_succeeded",
            actorUserId: user.Id,
            targetEntityId: user.Id,
            targetEntityType: nameof(ApplicationUser),
            description: "Password reset successful — all sessions and refresh tokens invalidated",
            ct);

        return Ok(new ResetPasswordResponse());
    }

    /// <summary>
    /// Reset the server-side inactivity timer for the caller's active session (AC-4).
    /// Returns the new session lifetime in seconds.
    /// Returns 401 when the session token is expired or not found.
    /// </summary>
    [Authorize]
    [HttpPost("session/extend")]
    [ProducesResponseType(typeof(ExtendSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExtendSession(
        [FromBody] ExtendSessionRequest request,
        CancellationToken ct)
    {
        try
        {
            await _sessionService.ExtendSessionAsync(request.SessionToken, ct);

            return Ok(new ExtendSessionResponse
            {
                ExpiresInSeconds = 900,
                Message = "Session extended successfully.",
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("ExtendSession rejected: {Reason}", ex.Message);
            return Unauthorized(new ProblemDetails
            {
                Title = "Session expired or not found.",
                Status = StatusCodes.Status401Unauthorized,
            });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a cryptographically random N-digit OTP string.
    /// </summary>
    private static string GenerateOtp(int digits = 6)
    {
        var buffer = new byte[4];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt32(buffer, 0) % (uint)Math.Pow(10, digits);
        return value.ToString($"D{digits}");
    }

    private async Task WriteAuditAsync(
        string eventType,
        Guid actorUserId,
        Guid? targetEntityId,
        string targetEntityType,
        string description,
        CancellationToken ct)
    {
        try
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                EventType = eventType,
                ActorUserId = actorUserId,
                TargetEntityId = targetEntityId,
                TargetEntityType = targetEntityType,
                OccurredAt = DateTimeOffset.UtcNow,
                Details = new AuditDetails
                {
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ChangeDescription = description
                }
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit logging must never fail the caller's request.
            _logger.LogError(ex, "Failed to write audit record for event {EventType}", eventType);
        }
    }
}

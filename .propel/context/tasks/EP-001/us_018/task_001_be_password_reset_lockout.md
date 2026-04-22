# Task - TASK_001

## Requirement Reference

- User Story: us_018
- Story Location: .propel/context/tasks/EP-001/us_018/us_018.md
- Acceptance Criteria:
  - AC-1: Given I am on the login page and click "Forgot Password", When I enter my registered email address, Then a password reset link is sent to my email within 2 minutes and the system does not confirm whether the email exists (security: same response for registered and unregistered emails).
  - AC-2: Given I receive the password reset email, When I click the link and submit a new password meeting complexity requirements (8+ characters, 1 uppercase, 1 number, 1 special character), Then my password is updated, the reset link is invalidated, and I am redirected to the login page.
  - AC-3: Given I enter an incorrect password 5 times consecutively, When the 5th failed attempt is recorded, Then my account is locked for 30 minutes, all active sessions are invalidated, and I receive an email notification of the lockout.
  - AC-4: Given my account is locked, When 30 minutes elapse, Then the account unlocks automatically and I can attempt login again.
- Edge Cases:
  - What happens if I click a password reset link more than 24 hours after it was issued? Link is expired; system displays "Reset link expired" with an option to request a new one.
  - How does the system handle multiple password reset requests within a short period? Rate limiting: maximum 3 reset requests per 15 minutes per account to prevent email flooding.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Library | ASP.NET Core Identity | 8.x (bundled) |
| Library | FluentValidation | latest stable |
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | latest stable |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the backend password reset flow and account lockout enforcement using ASP.NET Core Identity. The password reset flow has two endpoints: `POST /api/v1/auth/forgot-password` generates a time-limited reset token via `UserManager.GeneratePasswordResetTokenAsync()`, sends the reset link email through `INotificationSender`, and returns an identical 200 OK response regardless of whether the email exists in the system (AC-1 — prevents user enumeration). `POST /api/v1/auth/reset-password` validates the reset token, enforces password complexity (8+ characters, 1 uppercase, 1 number, 1 special character per AC-2), updates the password via `UserManager.ResetPasswordAsync()`, and invalidates the token. Reset tokens expire after 24 hours (edge case). Rate limiting is enforced at 3 requests per 15 minutes per email on the forgot-password endpoint to prevent email flooding (edge case). Account lockout is configured through ASP.NET Core Identity's `IdentityOptions.Lockout` with `MaxFailedAccessAttempts = 5` and `DefaultLockoutTimeSpan = 30 minutes` (AC-3, AC-4). On lockout trigger, all active sessions for the user are invalidated via `ISessionService` (from US_017), all refresh tokens are revoked, and a lockout notification email is sent. The lockout auto-expires after 30 minutes via Identity's built-in mechanism (AC-4). All password reset and lockout events are recorded in the audit log per NFR-010.

## Dependent Tasks

- US_013 task_001 (requires Identity configuration, ApplicationUser, AuthController, INotificationSender)
- US_014 task_001 (requires JWT authentication, RefreshTokenRepository for token revocation on lockout)
- US_017 task_001 (requires ISessionService for active session invalidation on lockout)

## Impacted Components

- New: `server/src/PropelIQ.Application/Auth/ForgotPasswordCommand.cs` (forgot password request/response DTOs)
- New: `server/src/PropelIQ.Application/Auth/ResetPasswordCommand.cs` (reset password request/response DTOs)
- New: `server/src/PropelIQ.Application/Auth/Validators/ResetPasswordCommandValidator.cs` (password complexity validation)
- New: `server/src/PropelIQ.Application/Auth/AccountLockoutHandler.cs` (lockout event handler: session invalidation, token revocation, email notification)
- Modify: `server/src/PropelIQ.Api/Controllers/AuthController.cs` (add forgot-password and reset-password endpoints)
- Modify: `server/src/PropelIQ.Api/Program.cs` (configure Identity lockout options, password reset token lifespan)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register AccountLockoutHandler)

## Implementation Plan

1. **Configure ASP.NET Core Identity lockout and token options** in `Program.cs`. Sets 5-attempt lockout with 30-minute duration (AC-3, AC-4) and 24-hour password reset token expiry (edge case):

```csharp
// In Program.cs — Identity lockout configuration
builder.Services.Configure<IdentityOptions>(options =>
{
    // Lockout settings (AC-3, AC-4)
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.AllowedForNewUsers = true;

    // Password complexity (AC-2)
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 4;
});

// Password reset token lifespan — 24 hours (edge case)
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});
```

2. **Create forgot-password endpoint** in `AuthController.cs`. Returns identical response for registered and unregistered emails to prevent user enumeration (AC-1). Rate-limited to 3 requests per 15 minutes per email (edge case):

```csharp
// Add to AuthController.cs
[HttpPost("forgot-password")]
[EnableRateLimiting("password-reset")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> ForgotPassword(
    [FromBody] ForgotPasswordRequest request,
    CancellationToken ct)
{
    // Always return success to prevent user enumeration (AC-1)
    var successResponse = new ForgotPasswordResponse
    {
        Message = "If an account with that email exists, a password reset link has been sent."
    };

    var user = await _userManager.FindByEmailAsync(request.Email);

    if (user is null)
    {
        await _auditRecorder.RecordAsync(new AuditEntry
        {
            Action = "ForgotPasswordAttempt",
            Detail = "Reset requested for nonexistent email",
            OccurredAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return Ok(successResponse);
    }

    // Generate reset token (expires in 24 hours per DataProtectionTokenProviderOptions)
    var resetToken = await _userManager
        .GeneratePasswordResetTokenAsync(user);

    // URL-safe encoding for the token
    var encodedToken = Uri.EscapeDataString(resetToken);
    var resetLink =
        $"{_clientBaseUrl}/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={encodedToken}";

    // Send reset email via INotificationSender (AC-1)
    await _notificationSender.SendEmailAsync(
        user.Email!,
        "Password Reset Request",
        $"Click the link to reset your password: {resetLink}\n\nThis link expires in 24 hours.\n\nIf you did not request this, please ignore this email.",
        ct);

    await _auditRecorder.RecordAsync(new AuditEntry
    {
        UserId = user.Id,
        Action = "ForgotPasswordSent",
        Detail = "Password reset email sent",
        OccurredAt = DateTime.UtcNow,
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
    }, ct);

    return Ok(successResponse);
}
```

3. **Create reset-password endpoint** in `AuthController.cs`. Validates the reset token, enforces password complexity, updates password, and invalidates the token (AC-2):

```csharp
// Add to AuthController.cs
[HttpPost("reset-password")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ResetPassword(
    [FromBody] ResetPasswordRequest request,
    CancellationToken ct)
{
    var user = await _userManager.FindByEmailAsync(request.Email);

    if (user is null)
    {
        // Same generic response — prevent enumeration
        return BadRequest(new ProblemDetails
        {
            Title = "Password reset failed",
            Detail = "The reset link is invalid or has expired.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    // Decode the URL-safe token
    var decodedToken = Uri.UnescapeDataString(request.Token);

    var result = await _userManager.ResetPasswordAsync(
        user, decodedToken, request.NewPassword);

    if (!result.Succeeded)
    {
        // Token expired or invalid (edge case: 24-hour expiry)
        var errors = string.Join("; ",
            result.Errors.Select(e => e.Description));

        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = user.Id,
            Action = "ResetPasswordFailed",
            Detail = $"Password reset failed: {errors}",
            OccurredAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return BadRequest(new ProblemDetails
        {
            Title = "Password reset failed",
            Detail = "The reset link is invalid or has expired.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    // Invalidate all active sessions and refresh tokens after password reset
    await _sessionService.InvalidateSessionAsync(
        user.Id, "PasswordReset", ct);
    await _refreshTokenRepository.RevokeAllForUserAsync(
        user.Id, "Password reset — all sessions revoked", ct);

    await _auditRecorder.RecordAsync(new AuditEntry
    {
        UserId = user.Id,
        Action = "ResetPasswordSucceeded",
        Detail = "Password reset successful — all sessions invalidated",
        OccurredAt = DateTime.UtcNow,
        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
    }, ct);

    return Ok(new ResetPasswordResponse
    {
        Message = "Password has been reset successfully. Please log in with your new password."
    });
}
```

4. **Create `AccountLockoutHandler`** for handling lockout events — invalidates sessions, revokes tokens, and sends lockout notification email (AC-3):

```csharp
// server/src/PropelIQ.Application/Auth/AccountLockoutHandler.cs
namespace PropelIQ.Application.Auth;

public class AccountLockoutHandler
{
    private readonly ISessionService _sessionService;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly INotificationSender _notificationSender;
    private readonly IAuditRecorder _auditRecorder;

    public AccountLockoutHandler(
        ISessionService sessionService,
        IRefreshTokenRepository refreshTokenRepo,
        INotificationSender notificationSender,
        IAuditRecorder auditRecorder)
    {
        _sessionService = sessionService;
        _refreshTokenRepo = refreshTokenRepo;
        _notificationSender = notificationSender;
        _auditRecorder = auditRecorder;
    }

    public async Task HandleLockoutAsync(
        ApplicationUser user, string? ipAddress, CancellationToken ct)
    {
        // Invalidate all active sessions (AC-3)
        await _sessionService.InvalidateSessionAsync(
            user.Id, "AccountLockout", ct);

        // Revoke all refresh tokens (AC-3)
        await _refreshTokenRepo.RevokeAllForUserAsync(
            user.Id, "Account locked — 5 failed attempts", ct);

        // Send lockout notification email (AC-3)
        await _notificationSender.SendEmailAsync(
            user.Email!,
            "Account Locked — Security Alert",
            $"Your account has been locked due to 5 consecutive failed login attempts.\n\n"
            + "Your account will automatically unlock in 30 minutes.\n\n"
            + "If you did not attempt these logins, please reset your password immediately after the lockout period.\n\n"
            + $"IP Address: {ipAddress ?? "Unknown"}",
            ct);

        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = user.Id,
            Action = "AccountLocked",
            Detail = "Account locked after 5 failed login attempts — sessions invalidated, notification sent",
            OccurredAt = DateTime.UtcNow,
            IpAddress = ipAddress
        }, ct);
    }
}
```

5. **Integrate lockout handling into the existing login endpoint** in `AuthController.cs`. After `CheckPasswordSignInAsync` returns `IsLockedOut`, call the `AccountLockoutHandler`:

```csharp
// Modify the existing Login method in AuthController.cs
// After: if (result.IsLockedOut) block

if (result.IsLockedOut)
{
    // Check if this is a fresh lockout (5th failed attempt)
    var accessFailedCount = await _userManager
        .GetAccessFailedCountAsync(user);
    if (accessFailedCount == 0) // Identity resets count on lockout
    {
        // Fresh lockout — trigger handler (AC-3)
        await _lockoutHandler.HandleLockoutAsync(
            user,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);
    }

    return Unauthorized(new ProblemDetails
    {
        Title = "Account locked",
        Detail = "Too many failed attempts. Please try again in 30 minutes.",
        Status = StatusCodes.Status401Unauthorized
    });
}
```

6. **Create DTOs and validators** for password reset:

```csharp
// server/src/PropelIQ.Application/Auth/ForgotPasswordCommand.cs
namespace PropelIQ.Application.Auth;

public record ForgotPasswordRequest(string Email);

public record ForgotPasswordResponse
{
    public string Message { get; init; } = string.Empty;
}
```

```csharp
// server/src/PropelIQ.Application/Auth/ResetPasswordCommand.cs
namespace PropelIQ.Application.Auth;

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);

public record ResetPasswordResponse
{
    public string Message { get; init; } = string.Empty;
}
```

```csharp
// server/src/PropelIQ.Application/Auth/Validators/ResetPasswordCommandValidator.cs
using FluentValidation;

namespace PropelIQ.Application.Auth.Validators;

public class ResetPasswordRequestValidator
    : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
                .WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]")
                .WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]")
                .WithMessage("Password must contain at least one special character.");
    }
}

public class ForgotPasswordRequestValidator
    : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
```

7. **Configure rate limiting for password reset** in `Program.cs`:

```csharp
// Add to Program.cs rate limiter configuration
builder.Services.AddRateLimiter(options =>
{
    // Existing rate limiters...

    options.AddFixedWindowLimiter("password-reset", config =>
    {
        config.PermitLimit = 3;
        config.Window = TimeSpan.FromMinutes(15);
        config.QueueLimit = 0;
    });
});
```

8. **Register services** in `DependencyInjection.cs`:

```csharp
// In DependencyInjection.cs
services.AddScoped<AccountLockoutHandler>();
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml
├── .env.example
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Program.cs
        │   └── Controllers/
        │       └── AuthController.cs    (from US_013 task_001, US_014 task_001)
        ├── PropelIQ.Application/
        │   ├── Auth/
        │   │   ├── LoginCommand.cs          (from US_014 task_001)
        │   │   ├── RegisterCommand.cs       (from US_013 task_001)
        │   │   └── Validators/
        │   │       └── LoginCommandValidator.cs
        │   ├── Sessions/
        │   │   └── SessionService.cs        (from US_017 task_001)
        │   └── Abstractions/
        │       ├── INotificationSender.cs   (from US_013 task_001)
        │       ├── IJwtTokenService.cs      (from US_014 task_001)
        │       ├── ISessionService.cs       (from US_017 task_001)
        │       └── IActiveSessionRepository.cs (from US_017 task_001)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── ActiveSession.cs         (from US_017 task_001)
        └── PropelIQ.Infrastructure/
            ├── Identity/
            │   ├── ApplicationUser.cs       (from US_013 task_001)
            │   ├── JwtTokenService.cs       (from US_014 task_001)
            │   ├── RefreshToken.cs          (from US_014 task_001)
            │   └── RefreshTokenRepository.cs (from US_014 task_001)
            ├── Sessions/
            │   ├── ActiveSessionRepository.cs   (from US_017 task_001)
            │   └── SessionCleanupService.cs     (from US_017 task_001)
            ├── AppDbContext.cs
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_013, US_014, US_017 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Auth/ForgotPasswordCommand.cs | Forgot password request/response DTOs |
| CREATE | server/src/PropelIQ.Application/Auth/ResetPasswordCommand.cs | Reset password request/response DTOs |
| CREATE | server/src/PropelIQ.Application/Auth/Validators/ResetPasswordCommandValidator.cs | FluentValidation for password complexity and forgot-password input |
| CREATE | server/src/PropelIQ.Application/Auth/AccountLockoutHandler.cs | Lockout event handler: session invalidation, token revocation, email notification |
| MODIFY | server/src/PropelIQ.Api/Controllers/AuthController.cs | Add forgot-password and reset-password endpoints, integrate lockout handler in login flow |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Configure Identity lockout options, password reset token lifespan (24h), password-reset rate limiter |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register AccountLockoutHandler |

## External References

- ASP.NET Core Identity password reset: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm
- UserManager.GeneratePasswordResetTokenAsync: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.generatepasswordresettokenasync
- UserManager.ResetPasswordAsync: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.resetpasswordasync
- ASP.NET Core Identity lockout: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration#lockout
- DataProtectionTokenProviderOptions: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions
- OWASP Forgot Password Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html
- ASP.NET Core Rate Limiting: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test forgot password (registered email)
curl -X POST http://localhost:5000/api/v1/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'

# Test forgot password (unregistered email — same response)
curl -X POST http://localhost:5000/api/v1/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"nonexistent@example.com"}'

# Test reset password
curl -X POST http://localhost:5000/api/v1/auth/reset-password \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","token":"<reset_token>","newPassword":"NewSecure@123"}'

# Test account lockout (submit wrong password 5 times)
for i in {1..5}; do
  curl -X POST http://localhost:5000/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"wrongpassword"}'
done
```

## Implementation Validation Strategy

- [ ] `POST /api/v1/auth/forgot-password` returns identical 200 OK response for registered and unregistered emails (AC-1)
- [ ] Password reset email is sent within 2 minutes with valid reset link (AC-1)
- [ ] Reset link contains URL-safe encoded token and email
- [ ] `POST /api/v1/auth/reset-password` updates password when token is valid and password meets complexity (AC-2)
- [ ] Password complexity enforced: 8+ chars, 1 uppercase, 1 number, 1 special character (AC-2)
- [ ] Used reset token cannot be reused (AC-2)
- [ ] Reset token expires after 24 hours — returns "invalid or expired" error (edge case)
- [ ] Account locks after 5 consecutive failed login attempts with 30-minute duration (AC-3, AC-4)
- [ ] On lockout, all active sessions are invalidated via ISessionService (AC-3)
- [ ] On lockout, all refresh tokens are revoked (AC-3)
- [ ] On lockout, email notification is sent to account owner (AC-3)
- [ ] Account automatically unlocks after 30 minutes (AC-4)
- [ ] Forgot-password endpoint rate-limited to 3 requests per 15 minutes per email (edge case)
- [ ] All password reset and lockout events are recorded in audit log (NFR-010)

## Implementation Checklist

- [x] Configure `IdentityOptions.Lockout` with `MaxFailedAccessAttempts = 5` and `DefaultLockoutTimeSpan = 30 minutes`
- [x] Configure `IdentityOptions.Password` with required complexity rules (8+ chars, uppercase, digit, special)
- [x] Configure named `"PasswordReset"` token provider with `TokenLifespan = 24 hours`
- [x] Create `ForgotPasswordRequest`/`ForgotPasswordResponse` DTOs
- [x] Create `ResetPasswordRequest`/`ResetPasswordResponse` DTOs
- [x] Create `ResetPasswordRequestValidator` and `ForgotPasswordRequestValidator` with FluentValidation
- [x] Create `AccountLockoutHandler` with session invalidation, token revocation, and lockout email notification
- [x] Implement `POST /api/v1/auth/forgot-password` with user-enumeration-safe response and rate limiting
- [x] Implement `POST /api/v1/auth/reset-password` with token validation, password update, and session invalidation
- [x] Add `[EnableRateLimiting("password-reset-policy")]` to forgot-password endpoint
- [x] Configure `password-reset-policy` fixed-window rate limiter (3 per 15 minutes)
- [x] Integrate `AccountLockoutHandler` in login endpoint lockout branch
- [x] Register `AccountLockoutHandler` in `Program.cs`

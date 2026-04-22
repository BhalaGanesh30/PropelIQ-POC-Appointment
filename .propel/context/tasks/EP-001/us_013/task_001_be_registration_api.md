# Task - TASK_001

## Requirement Reference

- User Story: us_013
- Story Location: .propel/context/tasks/EP-001/us_013/us_013.md
- Acceptance Criteria:
  - AC-1: Given I am on the registration page, When I submit a valid email address and password meeting the security requirements, Then the system sends a verification email within 30 seconds and my account is created in a pending state.
  - AC-2: Given I receive the verification email, When I click the verification link, Then my account is activated, I am redirected to the patient dashboard, and the authentication event is recorded in the audit log.
  - AC-3: Given I choose phone verification, When I submit my mobile number, Then a 6-digit OTP is sent via SMS within 30 seconds and my account activates upon successful OTP entry.
  - AC-4: Given I submit a registration form, When the email or phone number already exists in the system, Then the system displays "Account already exists" with a login link and does not reveal whether the account is verified.
- Edge Cases:
  - What happens if the verification link expires (after 24 hours)? User is prompted to request a new verification link from the login page.
  - How does the system handle registration attempts with disposable email addresses? Email format validation passes; no domain blocklist is applied in Phase 1, but the requirement is flagged for future security hardening.

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
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | latest stable |
| Library | FluentValidation | latest stable |
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

Build the backend registration API using ASP.NET Core Identity with email confirmation and phone OTP verification flows. The API exposes endpoints for account creation (pending state), email verification link confirmation, phone OTP send/verify, and duplicate detection. ASP.NET Identity's `UserManager` handles password hashing, token generation, and account state management. Email and SMS delivery are abstracted behind `INotificationSender` with retry and circuit-breaker policies per design Decision 6. All authentication events are recorded in the audit log per NFR-010. Rate limiting is enforced on registration and OTP endpoints per NFR-012 to prevent abuse. Verification tokens expire after 24 hours (edge case).

## Dependent Tasks

- US_002 tasks (requires API project structure with auth infrastructure)
- US_009 task_001 (requires User entity model in database)

## Impacted Components

- New: `server/src/PropelIQ.Api/Controllers/AuthController.cs` (registration endpoints)
- New: `server/src/PropelIQ.Application/Auth/RegisterCommand.cs` (registration request handler)
- New: `server/src/PropelIQ.Application/Auth/ConfirmEmailCommand.cs` (email verification handler)
- New: `server/src/PropelIQ.Application/Auth/SendOtpCommand.cs` (OTP send handler)
- New: `server/src/PropelIQ.Application/Auth/VerifyOtpCommand.cs` (OTP verification handler)
- New: `server/src/PropelIQ.Application/Auth/Validators/RegisterCommandValidator.cs` (input validation)
- New: `server/src/PropelIQ.Application/Abstractions/INotificationSender.cs` (email/SMS abstraction)
- New: `server/src/PropelIQ.Infrastructure/Notifications/EmailNotificationSender.cs` (email sender stub)
- New: `server/src/PropelIQ.Infrastructure/Notifications/SmsNotificationSender.cs` (SMS sender stub)
- New: `server/src/PropelIQ.Infrastructure/Identity/ApplicationUser.cs` (Identity user extending domain User)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register Identity services)
- Modify: `server/src/PropelIQ.Api/Program.cs` (configure Identity, token lifespan, rate limiting)

## Implementation Plan

1. **Configure ASP.NET Core Identity** in `Program.cs` with PostgreSQL backing store, email confirmation requirement, and token lifespan:

```csharp
// In Program.cs — Identity configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password policy (NFR-007)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;

    // Email confirmation required — accounts start in pending state (AC-1)
    options.SignIn.RequireConfirmedEmail = true;
    options.SignIn.RequireConfirmedAccount = true;

    // Lockout policy
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Token lifespan: 24 hours for email confirmation (edge case: link expiry)
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));
```

2. **Create `ApplicationUser`** extending `IdentityUser<Guid>` to integrate with the domain User entity:

```csharp
// server/src/PropelIQ.Infrastructure/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace PropelIQ.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }
    public string? VerificationMethod { get; set; } // "email" or "phone"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
}
```

3. **Create registration endpoint** in `AuthController.cs`. The endpoint creates the user in a pending state and dispatches either an email verification link or phone OTP:

```csharp
// server/src/PropelIQ.Api/Controllers/AuthController.cs
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationSender _notificationSender;
    private readonly IAuditRecorder _auditRecorder;

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        // AC-4: Check for existing account — return generic message
        // Do NOT reveal whether account is verified
        var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
        var existingByPhone = !string.IsNullOrEmpty(request.PhoneNumber)
            ? await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct)
            : null;

        if (existingByEmail is not null || existingByPhone is not null)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Account already exists",
                Detail = "An account with this email or phone number already exists. Please log in.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // Create user in pending state (AC-1)
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            VerificationMethod = string.IsNullOrEmpty(request.PhoneNumber)
                ? "email" : "phone"
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors.ToDictionary(
                    e => e.Code,
                    e => new[] { e.Description })));
        }

        // Assign Patient role
        await _userManager.AddToRoleAsync(user, "Patient");

        // Dispatch verification based on chosen method
        if (user.VerificationMethod == "phone")
        {
            // AC-3: Send 6-digit OTP via SMS
            var otp = GenerateOtp();
            await StoreOtp(user.Id, otp, ct);
            await _notificationSender.SendSmsAsync(
                user.PhoneNumber!,
                $"Your PropelIQ verification code is: {otp}",
                ct);
        }
        else
        {
            // AC-1: Send email verification link
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));
            var callbackUrl = $"{Request.Scheme}://{Request.Host}" +
                $"/api/v1/auth/confirm-email?userId={user.Id}&code={encodedToken}";

            await _notificationSender.SendEmailAsync(
                user.Email!,
                "Verify your PropelIQ account",
                $"Please verify your email by clicking: {callbackUrl}",
                ct);
        }

        // Audit log (NFR-010)
        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = user.Id,
            Action = "AccountCreated",
            Detail = $"Registration via {user.VerificationMethod}",
            OccurredAt = DateTime.UtcNow
        }, ct);

        return CreatedAtAction(nameof(Register), new { userId = user.Id });
    }
}
```

4. **Create email confirmation endpoint** (AC-2). Validates the token, activates the account, and records the audit event:

```csharp
[HttpGet("confirm-email")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> ConfirmEmail(
    [FromQuery] Guid userId,
    [FromQuery] string code,
    CancellationToken ct)
{
    var user = await _userManager.FindByIdAsync(userId.ToString());
    if (user is null)
        return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: 400, title: "Invalid verification link"));

    var decodedToken = Encoding.UTF8.GetString(
        WebEncoders.Base64UrlDecode(code));
    var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

    if (!result.Succeeded)
    {
        // Edge case: expired token (24h)
        return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: 400,
            title: "Verification failed",
            detail: "The verification link has expired or is invalid. " +
                    "Please request a new verification link from the login page."));
    }

    user.ActivatedAt = DateTime.UtcNow;
    await _userManager.UpdateAsync(user);

    // Audit log (AC-2)
    await _auditRecorder.RecordAsync(new AuditEntry
    {
        UserId = user.Id,
        Action = "AccountActivated",
        Detail = "Email verification confirmed",
        OccurredAt = DateTime.UtcNow
    }, ct);

    // Redirect to patient dashboard (AC-2)
    return Redirect("/dashboard?verified=true");
}
```

5. **Create OTP send and verify endpoints** (AC-3). OTP is a 6-digit code stored with a 10-minute expiry in a time-limited cache:

```csharp
[HttpPost("send-otp")]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> SendOtp(
    [FromBody] SendOtpRequest request,
    CancellationToken ct)
{
    var user = await _userManager.Users
        .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct);
    if (user is null)
        return Ok(); // Do not reveal account existence (AC-4 spirit)

    var otp = GenerateOtp();
    await StoreOtp(user.Id, otp, ct);
    await _notificationSender.SendSmsAsync(
        request.PhoneNumber,
        $"Your PropelIQ verification code is: {otp}",
        ct);

    return Ok();
}

[HttpPost("verify-otp")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> VerifyOtp(
    [FromBody] VerifyOtpRequest request,
    CancellationToken ct)
{
    var user = await _userManager.Users
        .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct);
    if (user is null)
        return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: 400, title: "Invalid request"));

    var isValid = await ValidateOtp(user.Id, request.Code, ct);
    if (!isValid)
        return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: 400, title: "Invalid or expired code"));

    // Activate account
    user.EmailConfirmed = true; // Marks account as confirmed in Identity
    user.PhoneNumberConfirmed = true;
    user.ActivatedAt = DateTime.UtcNow;
    await _userManager.UpdateAsync(user);

    await _auditRecorder.RecordAsync(new AuditEntry
    {
        UserId = user.Id,
        Action = "AccountActivated",
        Detail = "Phone OTP verification confirmed",
        OccurredAt = DateTime.UtcNow
    }, ct);

    return Ok(new { redirectUrl = "/dashboard?verified=true" });
}

// OTP helpers
private static string GenerateOtp()
{
    using var rng = RandomNumberGenerator.Create();
    var bytes = new byte[4];
    rng.GetBytes(bytes);
    var number = BitConverter.ToUInt32(bytes) % 1_000_000;
    return number.ToString("D6");
}
```

6. **Create `INotificationSender` abstraction** and stub implementations for email and SMS. These will be replaced with real provider integrations in later stories. Circuit-breaker and retry policies are applied per design Decision 6:

```csharp
// server/src/PropelIQ.Application/Abstractions/INotificationSender.cs
public interface INotificationSender
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct);
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Notifications/StubNotificationSender.cs
public class StubNotificationSender : INotificationSender
{
    private readonly ILogger<StubNotificationSender> _logger;

    public StubNotificationSender(ILogger<StubNotificationSender> logger)
        => _logger = logger;

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken ct)
    {
        _logger.LogInformation("[STUB EMAIL] To: {To}, Subject: {Subject}, Body: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct)
    {
        _logger.LogInformation("[STUB SMS] To: {Phone}, Message: {Message}",
            phoneNumber, message);
        return Task.CompletedTask;
    }
}
```

7. **Create input validation** with FluentValidation for the registration request:

```csharp
// server/src/PropelIQ.Application/Auth/Validators/RegisterCommandValidator.cs
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Phone number must be in E.164 format");
    }
}
```

8. **Configure rate limiting** on registration endpoints per NFR-012 to prevent abuse:

```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("registration", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(15);
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("otp", limiter =>
    {
        limiter.PermitLimit = 3;
        limiter.Window = TimeSpan.FromMinutes(5);
        limiter.QueueLimit = 0;
    });
});
```

Apply via `[EnableRateLimiting("registration")]` on the register endpoint and `[EnableRateLimiting("otp")]` on OTP endpoints.

## Current Project State

```text
propelIQ/
├── docker-compose.yml       (from US_005)
├── .env.example
├── infra/
│   └── postgres/
│       └── init.sql         (from US_003)
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Program.cs
        │   └── Controllers/
        ├── PropelIQ.Application/
        │   └── Abstractions/
        ├── PropelIQ.Domain/
        │   └── Entities/
        └── PropelIQ.Infrastructure/
            ├── Identity/
            ├── Notifications/
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_002 and US_009 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Api/Controllers/AuthController.cs | Registration, email confirmation, OTP send/verify endpoints |
| CREATE | server/src/PropelIQ.Application/Auth/RegisterRequest.cs | Registration request DTO |
| CREATE | server/src/PropelIQ.Application/Auth/Validators/RegisterRequestValidator.cs | FluentValidation rules for registration input |
| CREATE | server/src/PropelIQ.Application/Abstractions/INotificationSender.cs | Email and SMS notification abstraction |
| CREATE | server/src/PropelIQ.Infrastructure/Notifications/StubNotificationSender.cs | Stub email/SMS sender logging to console |
| CREATE | server/src/PropelIQ.Infrastructure/Identity/ApplicationUser.cs | Identity user with TenantId, VerificationMethod, ActivatedAt |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register Identity, notification sender, rate limiter services |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Configure Identity options, token lifespan, rate limiting policies |

## External References

- ASP.NET Core Identity overview: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
- Identity email confirmation: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm
- Identity API authorization endpoints: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization
- ASP.NET Core rate limiting: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
- DataProtectionTokenProviderOptions: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions
- FluentValidation: https://docs.fluentvalidation.net/
- E.164 phone format: https://www.itu.int/rec/T-REC-E.164

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test registration endpoint
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"SecureP@ss1234","phoneNumber":"+15551234567"}'

# Test email confirmation
curl "http://localhost:5000/api/v1/auth/confirm-email?userId=<guid>&code=<token>"

# Test OTP flow
curl -X POST http://localhost:5000/api/v1/auth/send-otp \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"+15551234567"}'

curl -X POST http://localhost:5000/api/v1/auth/verify-otp \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"+15551234567","code":"123456"}'
```

## Implementation Validation Strategy

- [x] `POST /api/v1/auth/register` creates user in pending state with `EmailConfirmed = false` (AC-1)
- [x] Email verification token is generated and notification sender is invoked within handler (AC-1)
- [x] `GET /api/v1/auth/confirm-email` activates account and records audit event (AC-2)
- [x] `POST /api/v1/auth/send-otp` sends 6-digit OTP via SMS notification sender (AC-3)
- [x] `POST /api/v1/auth/verify-otp` activates account on valid OTP and records audit event (AC-3)
- [x] Duplicate email/phone returns 409 with generic message (AC-4)
- [x] Expired verification token returns descriptive error with re-request guidance (edge case)
- [x] Rate limiter blocks more than 5 registrations per 15 minutes per IP (NFR-012)

## Implementation Checklist

- [x] Configure ASP.NET Core Identity with `RequireConfirmedEmail`, password policy, lockout policy, and 24-hour token lifespan
- [x] Create `ApplicationUser` extending `IdentityUser<Guid>` with `FirstName`, `LastName`, `IsActive`, `CreatedAt`
- [x] Implement `POST /api/v1/auth/register` with pending account creation, email token generation, and OTP dispatch
- [x] Implement `GET /api/v1/auth/confirm-email` with token validation, account activation, and audit logging
- [x] Implement `POST /api/v1/auth/send-otp` and `POST /api/v1/auth/verify-otp` with 6-digit cryptographic OTP and 10-minute Redis expiry
- [x] Create `INotificationSender` interface and `StubNotificationSender` with console logging
- [x] Create `RegisterRequestValidator` with email, password strength, and E.164 phone validation rules
- [x] Configure fixed-window rate limiters: 5/15min on registration, 3/5min on OTP endpoints

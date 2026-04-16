# Task - TASK_001

## Requirement Reference

- User Story: us_014
- Story Location: .propel/context/tasks/EP-001/us_014/us_014.md
- Acceptance Criteria:
  - AC-1: Given I am on the login page, When I enter valid credentials and submit, Then the system validates my credentials, issues a JWT access token and a refresh token, and redirects me to the role-appropriate dashboard within 500 ms.
  - AC-2: Given I am authenticated, When my access token expires, Then the system uses the refresh token to issue a new access token without requiring me to log in again.
  - AC-3: Given I submit invalid credentials, When the login request is processed, Then the system returns a generic "Invalid username or password" message (without distinguishing between wrong username vs. wrong password) and records the failed attempt.
  - AC-4: Given I log out, When the logout request is processed, Then the current JWT and refresh token are revoked server-side and I am redirected to the login page.
- Edge Cases:
  - What happens if I try to use a revoked refresh token? System rejects the token with HTTP 401 and logs the suspicious activity.
  - How does the system handle login from an unrecognized IP or device? Login proceeds; anomaly is flagged for future alerting but does not block access in Phase 1.

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
| Library | Microsoft.AspNetCore.Authentication.JwtBearer | 8.x |
| Library | System.IdentityModel.Tokens.Jwt | latest stable |
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

Implement the backend JWT authentication system with access token generation, refresh token rotation, server-side token revocation, and failed login attempt recording. The login endpoint validates credentials via ASP.NET Core Identity `SignInManager`, generates a short-lived JWT access token (15 minutes per NFR-008) containing role claims, and issues a long-lived opaque refresh token stored in the database. The refresh endpoint rotates the refresh token (one-time use) and issues a new access token without re-authentication. The logout endpoint revokes both tokens server-side. Failed login attempts are recorded in the audit log with a generic error response that does not distinguish between wrong username and wrong password (AC-3). Revoked refresh token reuse is detected and logged as suspicious activity (edge case). Unrecognized IP/device anomalies are logged but do not block access in Phase 1.

## Dependent Tasks

- US_013 task_001 (requires Identity configuration, ApplicationUser, and registration endpoints)
- US_002 tasks (requires API project structure with auth middleware skeleton)
- US_009 task_001 (requires User entity model)

## Impacted Components

- New: `server/src/PropelIQ.Infrastructure/Identity/JwtTokenService.cs` (JWT generation and validation)
- New: `server/src/PropelIQ.Infrastructure/Identity/RefreshToken.cs` (refresh token entity)
- New: `server/src/PropelIQ.Infrastructure/Identity/RefreshTokenRepository.cs` (refresh token persistence)
- New: `server/src/PropelIQ.Application/Auth/LoginCommand.cs` (login request/response DTOs)
- New: `server/src/PropelIQ.Application/Auth/Validators/LoginCommandValidator.cs` (input validation)
- New: `server/src/PropelIQ.Application/Abstractions/IJwtTokenService.cs` (token service abstraction)
- Modify: `server/src/PropelIQ.Api/Controllers/AuthController.cs` (add login, refresh, logout endpoints)
- Modify: `server/src/PropelIQ.Api/Program.cs` (configure JWT bearer authentication, token validation)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add RefreshToken DbSet)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register JWT services)

## Implementation Plan

1. **Configure JWT bearer authentication** in `Program.cs`. The access token lifetime is 15 minutes per NFR-008 session timeout requirement. The signing key is read from configuration (Vault-managed in production per design.md Security stack):

```csharp
// In Program.cs — JWT Bearer configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SigningKey"]!)),
        ClockSkew = TimeSpan.FromSeconds(30) // Tight clock skew
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Append(
                    "X-Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});
```

Configuration in `appsettings.json`:
```json
{
  "Jwt": {
    "Issuer": "PropelIQ",
    "Audience": "PropelIQ.Client",
    "SigningKey": "REPLACE_WITH_VAULT_MANAGED_KEY_MIN_256_BITS",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

2. **Create `RefreshToken` entity** for database-persisted refresh tokens with one-time-use rotation:

```csharp
// server/src/PropelIQ.Infrastructure/Identity/RefreshToken.cs
namespace PropelIQ.Infrastructure.Identity;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? RevokeReason { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
```

3. **Create `IJwtTokenService` and `JwtTokenService`** for JWT access token generation with role claims and refresh token creation:

```csharp
// server/src/PropelIQ.Application/Abstractions/IJwtTokenService.cs
public interface IJwtTokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Identity/JwtTokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PropelIQ.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
        => _settings = settings.Value;

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", user.TenantId?.ToString() ?? string.Empty),
        };

        // Add role claims for role-appropriate dashboard redirect (AC-1)
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress)
    {
        using var rng = RandomNumberGenerator.Create();
        var tokenBytes = new byte[64];
        rng.GetBytes(tokenBytes);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = Convert.ToBase64String(tokenBytes),
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            CreatedByIp = ipAddress
        };
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.SigningKey)),
            ValidateLifetime = false // Allow expired tokens for refresh
        };

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(token, validationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        return principal;
    }
}
```

4. **Create login endpoint** in `AuthController.cs`. Validates credentials, generates tokens, records audit events, and returns role-appropriate redirect hint:

```csharp
// Add to AuthController.cs
[HttpPost("login")]
[EnableRateLimiting("registration")]
[ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> Login(
    [FromBody] LoginRequest request,
    CancellationToken ct)
{
    // Find user (do NOT reveal which field is wrong — AC-3)
    var user = await _userManager.FindByEmailAsync(request.Email);

    if (user is null)
    {
        // Record failed attempt even for nonexistent accounts (AC-3)
        await _auditRecorder.RecordAsync(new AuditEntry
        {
            Action = "LoginFailed",
            Detail = "Invalid credentials — account not found",
            OccurredAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return Unauthorized(new ProblemDetails
        {
            Title = "Invalid username or password",
            Status = StatusCodes.Status401Unauthorized
        });
    }

    // Validate password via SignInManager
    var result = await _signInManager.CheckPasswordSignInAsync(
        user, request.Password, lockoutOnFailure: true);

    if (!result.Succeeded)
    {
        // Record failed attempt (AC-3)
        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = user.Id,
            Action = "LoginFailed",
            Detail = result.IsLockedOut
                ? "Account locked out"
                : "Invalid credentials",
            OccurredAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        if (result.IsLockedOut)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Account locked",
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

    // Generate tokens (AC-1)
    var roles = await _userManager.GetRolesAsync(user);
    var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
    var refreshToken = _jwtTokenService.GenerateRefreshToken(user.Id, ipAddress);

    // Persist refresh token
    await _refreshTokenRepository.AddAsync(refreshToken, ct);

    // Log unrecognized IP (edge case: anomaly flagging)
    // Phase 1: log only, do not block
    await _auditRecorder.RecordAsync(new AuditEntry
    {
        UserId = user.Id,
        Action = "LoginSucceeded",
        Detail = $"Role(s): {string.Join(",", roles)}",
        OccurredAt = DateTime.UtcNow,
        IpAddress = ipAddress
    }, ct);

    // Determine role-appropriate redirect (AC-1)
    var dashboardUrl = roles.FirstOrDefault() switch
    {
        "Admin" => "/admin/dashboard",
        "Staff" => "/staff/queue",
        "Clinician" => "/clinician/queue",
        _ => "/dashboard" // Patient default
    };

    return Ok(new LoginResponse
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken.Token,
        ExpiresIn = 900, // 15 minutes in seconds
        RedirectUrl = dashboardUrl
    });
}
```

5. **Create refresh token endpoint** (AC-2). Implements one-time-use rotation — the old refresh token is revoked and replaced:

```csharp
[HttpPost("refresh")]
[ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> RefreshToken(
    [FromBody] RefreshRequest request,
    CancellationToken ct)
{
    // Validate the expired access token to extract claims
    var principal = _jwtTokenService.GetPrincipalFromExpiredToken(
        request.AccessToken);
    if (principal is null)
        return Unauthorized(CreateProblem("Invalid token"));

    var userId = Guid.Parse(
        principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    // Find the refresh token in database
    var existingToken = await _refreshTokenRepository
        .GetByTokenAsync(request.RefreshToken, ct);

    if (existingToken is null || existingToken.UserId != userId)
        return Unauthorized(CreateProblem("Invalid refresh token"));

    // Edge case: revoked token reuse detection
    if (existingToken.IsRevoked)
    {
        // Suspicious activity — revoke all tokens for this user
        await _refreshTokenRepository.RevokeAllForUserAsync(
            userId, "Revoked token reuse detected", ct);

        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = userId,
            Action = "SuspiciousTokenReuse",
            Detail = "Revoked refresh token reused — all tokens revoked",
            OccurredAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return Unauthorized(CreateProblem("Token has been revoked"));
    }

    if (existingToken.IsExpired)
        return Unauthorized(CreateProblem("Refresh token expired"));

    // Rotate: revoke old, issue new
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
    var newRefreshToken = _jwtTokenService.GenerateRefreshToken(
        userId, ipAddress);

    existingToken.RevokedAt = DateTime.UtcNow;
    existingToken.RevokedByIp = ipAddress;
    existingToken.ReplacedByToken = newRefreshToken.Token;
    existingToken.RevokeReason = "Rotated";

    await _refreshTokenRepository.UpdateAsync(existingToken, ct);
    await _refreshTokenRepository.AddAsync(newRefreshToken, ct);

    // Issue new access token
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
```

6. **Create logout endpoint** (AC-4). Revokes the current refresh token server-side:

```csharp
[HttpPost("logout")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> Logout(
    [FromBody] LogoutRequest request,
    CancellationToken ct)
{
    var userId = Guid.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

    // Revoke the specific refresh token (AC-4)
    var token = await _refreshTokenRepository
        .GetByTokenAsync(request.RefreshToken, ct);

    if (token is not null && token.UserId == userId && token.IsActive)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;
        token.RevokeReason = "Logout";
        await _refreshTokenRepository.UpdateAsync(token, ct);
    }

    await _auditRecorder.RecordAsync(new AuditEntry
    {
        UserId = userId,
        Action = "Logout",
        Detail = "User logged out — tokens revoked",
        OccurredAt = DateTime.UtcNow,
        IpAddress = ipAddress
    }, ct);

    return Ok();
}
```

7. **Create `RefreshTokenRepository`** for database persistence of refresh tokens:

```csharp
// server/src/PropelIQ.Infrastructure/Identity/RefreshTokenRepository.cs
public class RefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
        => _context = context;

    public async Task AddAsync(RefreshToken token, CancellationToken ct)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<RefreshToken?> GetByTokenAsync(
        string token, CancellationToken ct)
        => await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task UpdateAsync(RefreshToken token, CancellationToken ct)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId, string reason, CancellationToken ct)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokeReason = reason;
        }

        await _context.SaveChangesAsync(ct);
    }
}
```

8. **Create DTOs and validator**:

```csharp
// LoginRequest / LoginResponse / RefreshRequest / LogoutRequest DTOs
public record LoginRequest(string Email, string Password);

public record LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string? RedirectUrl { get; init; }
}

public record RefreshRequest(string AccessToken, string RefreshToken);

public record LogoutRequest(string RefreshToken);
```

```csharp
// LoginRequestValidator
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml       (from US_005)
├── .env.example
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Program.cs
        │   └── Controllers/
        │       └── AuthController.cs    (from US_013 task_001)
        ├── PropelIQ.Application/
        │   ├── Auth/
        │   └── Abstractions/
        │       ├── INotificationSender.cs   (from US_013 task_001)
        │       └── IJwtTokenService.cs
        ├── PropelIQ.Domain/
        │   └── Entities/
        └── PropelIQ.Infrastructure/
            ├── Identity/
            │   └── ApplicationUser.cs   (from US_013 task_001)
            ├── AppDbContext.cs
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_013 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Infrastructure/Identity/JwtTokenService.cs | JWT access token generation with role claims, refresh token creation, expired token validation |
| CREATE | server/src/PropelIQ.Infrastructure/Identity/RefreshToken.cs | Refresh token entity with rotation tracking (ReplacedByToken, RevokedAt, RevokeReason) |
| CREATE | server/src/PropelIQ.Infrastructure/Identity/RefreshTokenRepository.cs | Refresh token CRUD and bulk revocation for suspicious reuse detection |
| CREATE | server/src/PropelIQ.Application/Auth/LoginCommand.cs | Login, refresh, logout DTOs |
| CREATE | server/src/PropelIQ.Application/Auth/Validators/LoginCommandValidator.cs | FluentValidation for login input |
| CREATE | server/src/PropelIQ.Application/Abstractions/IJwtTokenService.cs | Token service abstraction |
| MODIFY | server/src/PropelIQ.Api/Controllers/AuthController.cs | Add login, refresh, logout endpoints |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Configure AddAuthentication + AddJwtBearer with TokenValidationParameters |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add DbSet for RefreshToken |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register JwtTokenService, RefreshTokenRepository, JwtSettings |

## External References

- ASP.NET Core JWT bearer authentication: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication
- JwtSecurityToken class: https://learn.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
- TokenValidationParameters: https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters
- ASP.NET Core Identity SignInManager: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.signinmanager-1
- Refresh token rotation: https://auth0.com/docs/secure/tokens/refresh-tokens/refresh-token-rotation
- OWASP JWT best practices: https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test login endpoint
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"SecureP@ss1234"}'

# Test refresh token
curl -X POST http://localhost:5000/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"accessToken":"<expired_jwt>","refreshToken":"<refresh_token>"}'

# Test logout
curl -X POST http://localhost:5000/api/v1/auth/logout \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refresh_token>"}'

# Test invalid credentials (should return generic 401)
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"wrong@example.com","password":"wrong"}'
```

## Implementation Validation Strategy

- [ ] `POST /api/v1/auth/login` returns JWT access token and refresh token on valid credentials (AC-1)
- [ ] Access token contains `sub`, `email`, `role`, `tenant_id`, and `jti` claims with 15-minute expiry
- [ ] `POST /api/v1/auth/refresh` returns new access token and rotated refresh token (AC-2)
- [ ] Invalid credentials return generic "Invalid username or password" without distinguishing cause (AC-3)
- [ ] Failed login attempt is recorded in audit log with IP address (AC-3)
- [ ] `POST /api/v1/auth/logout` revokes refresh token server-side and records audit event (AC-4)
- [ ] Reuse of a revoked refresh token triggers bulk revocation and suspicious activity log (edge case)
- [ ] Login from unrecognized IP is logged but not blocked (edge case — Phase 1)

## Implementation Checklist

- [ ] Configure JWT bearer authentication with `AddJwtBearer`, `TokenValidationParameters`, 30s clock skew, and `X-Token-Expired` header
- [ ] Create `RefreshToken` entity with one-time-use rotation fields (ReplacedByToken, RevokedAt, RevokeReason, CreatedByIp)
- [ ] Create `JwtTokenService` with `GenerateAccessToken` (role claims, 15min expiry), `GenerateRefreshToken` (64-byte crypto random), `GetPrincipalFromExpiredToken`
- [ ] Implement `POST /api/v1/auth/login` with SignInManager validation, lockout support, generic error response, and audit logging
- [ ] Implement `POST /api/v1/auth/refresh` with token rotation, revoked-token-reuse detection, and bulk revocation
- [ ] Implement `POST /api/v1/auth/logout` with server-side refresh token revocation and audit event
- [ ] Create `RefreshTokenRepository` with `AddAsync`, `GetByTokenAsync`, `UpdateAsync`, `RevokeAllForUserAsync`
- [ ] Add `Jwt` configuration section to `appsettings.json` and register `JwtSettings` options with `ValidateOnStart`

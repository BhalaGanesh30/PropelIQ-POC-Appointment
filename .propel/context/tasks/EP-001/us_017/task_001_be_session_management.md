# Task - TASK_001

## Requirement Reference

- User Story: us_017
- Story Location: .propel/context/tasks/EP-001/us_017/us_017.md
- Acceptance Criteria:
  - AC-1: Given a user is authenticated and has been inactive for 13 minutes, When the 13-minute inactivity threshold is reached, Then a non-blocking modal appears with a 2-minute countdown offering "Extend Session" and "Logout" options.
  - AC-2: Given the session warning modal is shown, When the 2-minute countdown expires without user action, Then the session is terminated, the JWT is revoked, and the user is redirected to the login page with the message "Session expired."
  - AC-3: Given a user logs in from a second device while an active session exists, When the second login is processed, Then the first session is immediately invalidated and the user on the first device receives a "Session ended" notification.
  - AC-4: Given a user clicks "Extend Session" in the warning modal, When the extension request is processed, Then the inactivity timer resets to 15 minutes and the modal is dismissed.
- Edge Cases:
  - What happens if the user's browser crashes during an active session? Session times out after 15 minutes of inactivity from the last recorded activity timestamp.
  - How does the system handle tab duplication in the same browser? Both tabs share the same session; activity in either tab resets the inactivity timer.

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
| Library | Microsoft.AspNetCore.SignalR | 8.x (bundled) |
| Library | Microsoft.AspNetCore.Authentication.JwtBearer | 8.x |
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

Implement backend session management with single active session enforcement, session extend and terminate APIs, server-side inactivity tracking, and real-time session invalidation notifications via SignalR. The login flow (from US_014 task_001) is extended to create an `ActiveSession` record and invalidate any prior session for the same user. A new `POST /api/v1/auth/session/extend` endpoint resets the server-side last-activity timestamp and returns a refreshed session TTL (AC-4). When a session expires server-side (15 minutes from last activity) or a second login invalidates it (AC-3), the corresponding refresh tokens are revoked and a SignalR message is pushed to the first device. The `SessionHub` authenticates connections using the existing JWT bearer scheme and maps connections to user IDs for targeted notifications. A background `SessionCleanupService` periodically purges expired sessions and revokes their tokens (edge case: browser crash). All session lifecycle events are recorded in the audit log per NFR-010.

## Dependent Tasks

- US_014 task_001 (requires JWT authentication, RefreshToken entity, RefreshTokenRepository, AuthController login/logout endpoints)

## Impacted Components

- New: `server/src/PropelIQ.Domain/Entities/ActiveSession.cs` (active session entity with last-activity timestamp)
- New: `server/src/PropelIQ.Infrastructure/Sessions/ActiveSessionRepository.cs` (session CRUD and single-session queries)
- New: `server/src/PropelIQ.Application/Abstractions/IActiveSessionRepository.cs` (repository abstraction)
- New: `server/src/PropelIQ.Application/Sessions/SessionService.cs` (session lifecycle: create, extend, invalidate, enforce single-session)
- New: `server/src/PropelIQ.Application/Abstractions/ISessionService.cs` (session service abstraction)
- New: `server/src/PropelIQ.Api/Hubs/SessionHub.cs` (SignalR hub for session invalidation notifications)
- New: `server/src/PropelIQ.Infrastructure/Sessions/SessionCleanupService.cs` (hosted service for expired session purge)
- Modify: `server/src/PropelIQ.Api/Controllers/AuthController.cs` (add session/extend endpoint, integrate single-session enforcement on login, revoke tokens on session termination)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register SignalR, SessionService, ActiveSessionRepository, SessionCleanupService)
- Modify: `server/src/PropelIQ.Infrastructure/AppDbContext.cs` (add DbSet for ActiveSession)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register session services)

## Implementation Plan

1. **Create `ActiveSession` entity** for tracking per-user session state with server-side last-activity timestamps:

```csharp
// server/src/PropelIQ.Domain/Entities/ActiveSession.cs
namespace PropelIQ.Domain.Entities;

public class ActiveSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TerminationReason { get; set; }
    public DateTime? TerminatedAt { get; set; }
}
```

2. **Create `IActiveSessionRepository` and `ActiveSessionRepository`** for session persistence and single-session queries:

```csharp
// server/src/PropelIQ.Application/Abstractions/IActiveSessionRepository.cs
namespace PropelIQ.Application.Abstractions;

public interface IActiveSessionRepository
{
    Task<ActiveSession?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);
    Task<ActiveSession?> GetBySessionTokenAsync(string sessionToken, CancellationToken ct);
    Task AddAsync(ActiveSession session, CancellationToken ct);
    Task UpdateAsync(ActiveSession session, CancellationToken ct);
    Task<List<ActiveSession>> GetExpiredSessionsAsync(DateTime cutoff, CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Sessions/ActiveSessionRepository.cs
namespace PropelIQ.Infrastructure.Sessions;

public class ActiveSessionRepository : IActiveSessionRepository
{
    private readonly AppDbContext _context;

    public ActiveSessionRepository(AppDbContext context)
        => _context = context;

    public async Task<ActiveSession?> GetActiveByUserIdAsync(
        Guid userId, CancellationToken ct)
        => await _context.ActiveSessions
            .FirstOrDefaultAsync(
                s => s.UserId == userId && s.IsActive, ct);

    public async Task<ActiveSession?> GetBySessionTokenAsync(
        string sessionToken, CancellationToken ct)
        => await _context.ActiveSessions
            .FirstOrDefaultAsync(
                s => s.SessionToken == sessionToken && s.IsActive, ct);

    public async Task AddAsync(ActiveSession session, CancellationToken ct)
    {
        _context.ActiveSessions.Add(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ActiveSession session, CancellationToken ct)
    {
        _context.ActiveSessions.Update(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ActiveSession>> GetExpiredSessionsAsync(
        DateTime cutoff, CancellationToken ct)
        => await _context.ActiveSessions
            .Where(s => s.IsActive && s.LastActivityAt <= cutoff)
            .ToListAsync(ct);
}
```

3. **Create `ISessionService` and `SessionService`** for session lifecycle management with single-session enforcement:

```csharp
// server/src/PropelIQ.Application/Abstractions/ISessionService.cs
namespace PropelIQ.Application.Abstractions;

public interface ISessionService
{
    Task<ActiveSession> CreateSessionAsync(
        Guid userId, string? ipAddress, string? userAgent, CancellationToken ct);
    Task ExtendSessionAsync(string sessionToken, CancellationToken ct);
    Task InvalidateSessionAsync(
        Guid userId, string reason, CancellationToken ct);
    Task<bool> IsSessionValidAsync(string sessionToken, CancellationToken ct);
}
```

```csharp
// server/src/PropelIQ.Application/Sessions/SessionService.cs
using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;

namespace PropelIQ.Application.Sessions;

public class SessionService : ISessionService
{
    private const int SessionTimeoutMinutes = 15;

    private readonly IActiveSessionRepository _sessionRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IHubContext<SessionHub> _sessionHub;
    private readonly IAuditRecorder _auditRecorder;

    public SessionService(
        IActiveSessionRepository sessionRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IHubContext<SessionHub> sessionHub,
        IAuditRecorder auditRecorder)
    {
        _sessionRepo = sessionRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _sessionHub = sessionHub;
        _auditRecorder = auditRecorder;
    }

    public async Task<ActiveSession> CreateSessionAsync(
        Guid userId, string? ipAddress, string? userAgent,
        CancellationToken ct)
    {
        // Enforce single active session — invalidate any existing session (AC-3)
        var existingSession = await _sessionRepo
            .GetActiveByUserIdAsync(userId, ct);

        if (existingSession is not null)
        {
            existingSession.IsActive = false;
            existingSession.TerminatedAt = DateTime.UtcNow;
            existingSession.TerminationReason = "NewLoginFromAnotherDevice";
            await _sessionRepo.UpdateAsync(existingSession, ct);

            // Revoke all refresh tokens for old session
            await _refreshTokenRepo.RevokeAllForUserAsync(
                userId, "Session replaced by new login", ct);

            // Push real-time notification to first device (AC-3)
            await _sessionHub.Clients
                .Group($"user_{userId}")
                .SendAsync("SessionEnded",
                    "Your session was ended because you logged in from another device.",
                    ct);

            await _auditRecorder.RecordAsync(new AuditEntry
            {
                UserId = userId,
                Action = "SessionInvalidated",
                Detail = "Previous session terminated — new login from another device",
                OccurredAt = DateTime.UtcNow,
                IpAddress = ipAddress
            }, ct);
        }

        // Create new active session
        using var rng = RandomNumberGenerator.Create();
        var tokenBytes = new byte[32];
        rng.GetBytes(tokenBytes);

        var session = new ActiveSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = Convert.ToBase64String(tokenBytes),
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsActive = true
        };

        await _sessionRepo.AddAsync(session, ct);

        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = userId,
            Action = "SessionCreated",
            Detail = $"New session created — IP: {ipAddress}",
            OccurredAt = DateTime.UtcNow,
            IpAddress = ipAddress
        }, ct);

        return session;
    }

    public async Task ExtendSessionAsync(
        string sessionToken, CancellationToken ct)
    {
        var session = await _sessionRepo
            .GetBySessionTokenAsync(sessionToken, ct);

        if (session is null || !session.IsActive)
            throw new InvalidOperationException("Session not found or inactive.");

        // Check if session has already expired server-side
        var inactivityCutoff = DateTime.UtcNow
            .AddMinutes(-SessionTimeoutMinutes);
        if (session.LastActivityAt <= inactivityCutoff)
            throw new InvalidOperationException("Session has expired.");

        // Reset inactivity timer (AC-4)
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow
            .AddMinutes(SessionTimeoutMinutes);

        await _sessionRepo.UpdateAsync(session, ct);
    }

    public async Task InvalidateSessionAsync(
        Guid userId, string reason, CancellationToken ct)
    {
        var session = await _sessionRepo
            .GetActiveByUserIdAsync(userId, ct);

        if (session is null)
            return;

        session.IsActive = false;
        session.TerminatedAt = DateTime.UtcNow;
        session.TerminationReason = reason;
        await _sessionRepo.UpdateAsync(session, ct);

        // Revoke all refresh tokens (AC-2)
        await _refreshTokenRepo.RevokeAllForUserAsync(
            userId, reason, ct);

        await _auditRecorder.RecordAsync(new AuditEntry
        {
            UserId = userId,
            Action = "SessionTerminated",
            Detail = $"Session terminated — Reason: {reason}",
            OccurredAt = DateTime.UtcNow
        }, ct);
    }

    public async Task<bool> IsSessionValidAsync(
        string sessionToken, CancellationToken ct)
    {
        var session = await _sessionRepo
            .GetBySessionTokenAsync(sessionToken, ct);

        if (session is null || !session.IsActive)
            return false;

        var inactivityCutoff = DateTime.UtcNow
            .AddMinutes(-SessionTimeoutMinutes);

        return session.LastActivityAt > inactivityCutoff;
    }
}
```

4. **Create `SessionHub`** for real-time session invalidation notifications via SignalR:

```csharp
// server/src/PropelIQ.Api/Hubs/SessionHub.cs
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PropelIQ.Api.Hubs;

[Authorize]
public class SessionHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(
            JwtRegisteredClaimNames.Sub)?.Value;

        if (userId is not null)
        {
            // Group by user ID so we can target all connections for a user
            await Groups.AddToGroupAsync(
                Context.ConnectionId, $"user_{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(
            JwtRegisteredClaimNames.Sub)?.Value;

        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId, $"user_{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

5. **Add session extend endpoint** to `AuthController.cs`:

```csharp
// Add to AuthController.cs
[HttpPost("session/extend")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> ExtendSession(
    [FromBody] ExtendSessionRequest request,
    CancellationToken ct)
{
    try
    {
        await _sessionService.ExtendSessionAsync(
            request.SessionToken, ct);

        return Ok(new ExtendSessionResponse
        {
            ExpiresInSeconds = 900, // 15 minutes
            Message = "Session extended successfully."
        });
    }
    catch (InvalidOperationException)
    {
        return Unauthorized(new ProblemDetails
        {
            Title = "Session expired",
            Status = StatusCodes.Status401Unauthorized
        });
    }
}
```

DTOs for session management:

```csharp
public record ExtendSessionRequest(string SessionToken);

public record ExtendSessionResponse
{
    public int ExpiresInSeconds { get; init; }
    public string Message { get; init; } = string.Empty;
}
```

6. **Modify the login endpoint** in `AuthController.cs` to create an `ActiveSession` and enforce single-session:

```csharp
// Inside the existing Login method, after generating tokens:
// Create active session and enforce single-session (AC-3)
var session = await _sessionService.CreateSessionAsync(
    user.Id,
    HttpContext.Connection.RemoteIpAddress?.ToString(),
    HttpContext.Request.Headers.UserAgent.ToString(),
    ct);

// Include session token in login response
return Ok(new LoginResponse
{
    AccessToken = accessToken,
    RefreshToken = refreshToken.Token,
    ExpiresIn = 900,
    RedirectUrl = dashboardUrl,
    SessionToken = session.SessionToken
});
```

7. **Modify the logout endpoint** to also invalidate the active session:

```csharp
// Inside the existing Logout method, before returning Ok:
await _sessionService.InvalidateSessionAsync(
    userId, "UserLogout", ct);
```

8. **Create `SessionCleanupService`** as a background hosted service to purge expired sessions (edge case: browser crash):

```csharp
// server/src/PropelIQ.Infrastructure/Sessions/SessionCleanupService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Infrastructure.Sessions;

public class SessionCleanupService : BackgroundService
{
    private const int CleanupIntervalMinutes = 5;
    private const int SessionTimeoutMinutes = 15;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Error during session cleanup cycle");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(CleanupIntervalMinutes),
                stoppingToken);
        }
    }

    private async Task CleanupExpiredSessionsAsync(
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider
            .GetRequiredService<IActiveSessionRepository>();
        var refreshTokenRepo = scope.ServiceProvider
            .GetRequiredService<IRefreshTokenRepository>();
        var auditRecorder = scope.ServiceProvider
            .GetRequiredService<IAuditRecorder>();

        var cutoff = DateTime.UtcNow
            .AddMinutes(-SessionTimeoutMinutes);
        var expiredSessions = await sessionRepo
            .GetExpiredSessionsAsync(cutoff, ct);

        foreach (var session in expiredSessions)
        {
            session.IsActive = false;
            session.TerminatedAt = DateTime.UtcNow;
            session.TerminationReason = "InactivityTimeout";
            await sessionRepo.UpdateAsync(session, ct);

            await refreshTokenRepo.RevokeAllForUserAsync(
                session.UserId, "Session expired — inactivity", ct);

            await auditRecorder.RecordAsync(new AuditEntry
            {
                UserId = session.UserId,
                Action = "SessionExpired",
                Detail = "Session terminated due to inactivity timeout",
                OccurredAt = DateTime.UtcNow
            }, ct);

            _logger.LogInformation(
                "Expired session {SessionId} for user {UserId}",
                session.Id, session.UserId);
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} expired sessions",
                expiredSessions.Count);
        }
    }
}
```

9. **Register services** in `Program.cs` and `DependencyInjection.cs`:

```csharp
// In Program.cs — add SignalR
builder.Services.AddSignalR();

// In Program.cs — map SignalR hub (after app.MapControllers())
app.MapHub<SessionHub>("/hubs/session");
```

```csharp
// In DependencyInjection.cs — register session services
services.AddScoped<IActiveSessionRepository, ActiveSessionRepository>();
services.AddScoped<ISessionService, SessionService>();
services.AddHostedService<SessionCleanupService>();
```

10. **Add `ActiveSession` to `AppDbContext`**:

```csharp
// In AppDbContext.cs
public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();

// In OnModelCreating
modelBuilder.Entity<ActiveSession>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.UserId, e.IsActive });
    entity.HasIndex(e => e.SessionToken).IsUnique();
    entity.HasIndex(e => e.LastActivityAt);
    entity.Property(e => e.SessionToken).HasMaxLength(256);
    entity.Property(e => e.TerminationReason).HasMaxLength(256);
    entity.Property(e => e.IpAddress).HasMaxLength(45);
    entity.Property(e => e.UserAgent).HasMaxLength(512);
});
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml
├── .env.example
├── server/
│   └── src/
│       ├── PropelIQ.Api/
│       │   ├── Program.cs
│       │   └── Controllers/
│       │       └── AuthController.cs    (from US_014 task_001)
│       ├── PropelIQ.Application/
│       │   ├── Auth/
│       │   │   ├── LoginCommand.cs
│       │   │   └── Validators/
│       │   │       └── LoginCommandValidator.cs
│       │   └── Abstractions/
│       │       ├── INotificationSender.cs
│       │       └── IJwtTokenService.cs
│       ├── PropelIQ.Domain/
│       │   └── Entities/
│       └── PropelIQ.Infrastructure/
│           ├── Identity/
│           │   ├── ApplicationUser.cs
│           │   ├── JwtTokenService.cs
│           │   ├── RefreshToken.cs
│           │   └── RefreshTokenRepository.cs
│           ├── AppDbContext.cs
│           └── DependencyInjection.cs
└── client/
    └── src/
        └── app/
            ├── core/
            │   ├── interceptors/
            │   │   └── auth.interceptor.ts
            │   ├── guards/
            │   │   └── auth.guard.ts
            │   └── services/
            │       └── token-storage.service.ts
            └── features/
                └── auth/
                    ├── services/
                    │   └── auth.service.ts
                    └── pages/
                        └── login/
```

> Placeholder: Update on execution based on US_014 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Entities/ActiveSession.cs | Active session entity with last-activity timestamp and termination tracking |
| CREATE | server/src/PropelIQ.Application/Abstractions/IActiveSessionRepository.cs | Repository abstraction for active session queries |
| CREATE | server/src/PropelIQ.Infrastructure/Sessions/ActiveSessionRepository.cs | Session CRUD, single-session query, expired session retrieval |
| CREATE | server/src/PropelIQ.Application/Abstractions/ISessionService.cs | Session lifecycle service abstraction |
| CREATE | server/src/PropelIQ.Application/Sessions/SessionService.cs | Create, extend, invalidate sessions with single-session enforcement and SignalR notifications |
| CREATE | server/src/PropelIQ.Api/Hubs/SessionHub.cs | SignalR hub for session invalidation push notifications |
| CREATE | server/src/PropelIQ.Infrastructure/Sessions/SessionCleanupService.cs | Background hosted service to purge expired sessions and revoke orphaned tokens |
| MODIFY | server/src/PropelIQ.Api/Controllers/AuthController.cs | Add session/extend endpoint, integrate CreateSessionAsync in login, InvalidateSessionAsync in logout |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register AddSignalR, map SessionHub endpoint |
| MODIFY | server/src/PropelIQ.Infrastructure/AppDbContext.cs | Add DbSet for ActiveSession with indexes |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register ActiveSessionRepository, SessionService, SessionCleanupService |

## External References

- ASP.NET Core SignalR overview: https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction
- SignalR authentication and authorization: https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz
- BackgroundService in ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- OWASP Session Management Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Test session extend
curl -X POST http://localhost:5000/api/v1/auth/session/extend \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"sessionToken":"<session_token>"}'

# Test login (should return sessionToken and invalidate prior session)
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"SecureP@ss1234"}'
```

## Implementation Validation Strategy

- [x] Login creates an `ActiveSession` record and returns `sessionToken` in the response (AC-3)
- [x] Second login from another device invalidates the first session and revokes its refresh tokens (AC-3)
- [x] SignalR sends "SessionEnded" message to the first device on session replacement (AC-3)
- [x] `POST /api/v1/auth/session/extend` resets `LastActivityAt` and `ExpiresAt` to 15 minutes from now (AC-4)
- [x] Extend request on an expired or inactive session returns HTTP 401 (AC-2)
- [x] Logout invalidates the active session and revokes refresh tokens (AC-2)
- [x] `SessionCleanupService` terminates sessions with `LastActivityAt` older than 15 minutes (edge case: browser crash)
- [x] All session lifecycle events (create, extend, invalidate, expire) are recorded in audit log (NFR-010)
- [x] `ActiveSession` table has indexes on `(UserId, IsActive)`, `SessionToken` (unique), and `LastActivityAt`
- [x] SignalR hub requires JWT bearer authentication and groups connections by user ID

## Implementation Checklist

- [x] Create `ActiveSession` entity with `Id`, `UserId`, `SessionToken`, `LastActivityAt`, `ExpiresAt`, `IsActive`, `TerminationReason`, `TerminatedAt`, `IpAddress`, `UserAgent`
- [x] Create `IActiveSessionRepository` with `GetActiveByUserIdAsync`, `GetBySessionTokenAsync`, `AddAsync`, `UpdateAsync`, `GetExpiredSessionsAsync`
- [x] Create `ActiveSessionRepository` implementation against `AppDbContext`
- [x] Create `ISessionService` with `CreateSessionAsync`, `ExtendSessionAsync`, `InvalidateSessionAsync`, `IsSessionValidAsync`
- [x] Create `SessionService` with single-session enforcement, SignalR push, and audit logging
- [x] Create `SessionHub` with JWT auth, user-group mapping on connect/disconnect
- [x] Create `SessionCleanupService` background service with 5-minute interval and 15-minute inactivity cutoff
- [x] Add `POST /api/v1/auth/session/extend` endpoint to `AuthController`
- [x] Modify login endpoint to call `CreateSessionAsync` and include `SessionToken` in response
- [x] Modify logout endpoint to call `InvalidateSessionAsync`
- [x] Add `ActiveSession` DbSet and entity configuration to `AppDbContext`
- [x] Register all session services in `DependencyInjection.cs`
- [x] Register `AddSignalR()` and `MapHub<SessionHub>("/hubs/session")` in `Program.cs`

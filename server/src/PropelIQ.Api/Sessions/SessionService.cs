using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;

namespace PropelIQ.Api.Sessions;

/// <summary>
/// Session lifecycle service: create, extend, validate, and invalidate sessions.
/// Enforces the single-session constraint (AC-3) and records all lifecycle events
/// in the audit log (NFR-010).
/// </summary>
public sealed class SessionService : ISessionService
{
    private const int SessionTimeoutMinutes = 15;

    private readonly IActiveSessionRepository _sessionRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IHubContext<Hubs.SessionHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        IActiveSessionRepository sessionRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IHubContext<Hubs.SessionHub> hubContext,
        AppDbContext db,
        ILogger<SessionService> logger)
    {
        _sessionRepo = sessionRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
    }

    public async Task<ActiveSession> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        // Enforce single active session — invalidate any existing session (AC-3).
        var existing = await _sessionRepo.GetActiveByUserIdAsync(userId, ct);
        if (existing is not null)
        {
            existing.IsActive = false;
            existing.TerminatedAt = DateTime.UtcNow;
            existing.TerminationReason = "NewLoginFromAnotherDevice";
            await _sessionRepo.UpdateAsync(existing, ct);

            // Revoke all refresh tokens for the displaced session.
            await _refreshTokenRepo.RevokeAllForUserAsync(
                userId, "Session replaced by new login", ct);

            // Push real-time notification so the displaced device can react (AC-3).
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync(
                    "SessionEnded",
                    "Your session was ended because you logged in from another device.",
                    ct);

            await WriteAuditAsync(
                eventType: "session.displaced",
                actorUserId: userId,
                description: "Previous session terminated — new login from another device",
                ipAddress: ipAddress,
                ct);
        }

        // Generate a cryptographically secure opaque session token.
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);

        var session = new ActiveSession
        {
            UserId = userId,
            SessionToken = Convert.ToBase64String(tokenBytes),
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        await _sessionRepo.AddAsync(session, ct);

        await WriteAuditAsync(
            eventType: "session.created",
            actorUserId: userId,
            description: $"New session created. IP: {ipAddress}",
            ipAddress: ipAddress,
            ct);

        _logger.LogInformation("Session created for user {UserId}", userId);
        return session;
    }

    public async Task ExtendSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetBySessionTokenAsync(sessionToken, ct);
        if (session is null || !session.IsActive)
            throw new InvalidOperationException("Session not found or inactive.");

        // Reject if inactivity window already elapsed server-side.
        if (session.LastActivityAt <= DateTime.UtcNow.AddMinutes(-SessionTimeoutMinutes))
            throw new InvalidOperationException("Session has expired.");

        // Reset the inactivity timer (AC-4).
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTimeoutMinutes);
        await _sessionRepo.UpdateAsync(session, ct);

        _logger.LogDebug("Session {SessionId} extended for user {UserId}",
            session.Id, session.UserId);
    }

    public async Task InvalidateSessionAsync(
        Guid userId, string reason, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetActiveByUserIdAsync(userId, ct);
        if (session is null) return;

        session.IsActive = false;
        session.TerminatedAt = DateTime.UtcNow;
        session.TerminationReason = reason;
        await _sessionRepo.UpdateAsync(session, ct);

        await _refreshTokenRepo.RevokeAllForUserAsync(userId, reason, ct);

        await WriteAuditAsync(
            eventType: "session.terminated",
            actorUserId: userId,
            description: $"Session terminated. Reason: {reason}",
            ipAddress: null,
            ct);
    }

    public async Task<bool> IsSessionValidAsync(string sessionToken, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetBySessionTokenAsync(sessionToken, ct);
        if (session is null || !session.IsActive) return false;
        return session.LastActivityAt > DateTime.UtcNow.AddMinutes(-SessionTimeoutMinutes);
    }

    // ── Audit helper ──────────────────────────────────────────────────────────

    private async Task WriteAuditAsync(
        string eventType,
        Guid actorUserId,
        string description,
        string? ipAddress,
        CancellationToken ct)
    {
        try
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                EventType = eventType,
                ActorUserId = actorUserId,
                TargetEntityId = actorUserId,
                TargetEntityType = "ActiveSession",
                OccurredAt = DateTimeOffset.UtcNow,
                Details = new AuditDetails
                {
                    IpAddress = ipAddress,
                    ChangeDescription = description,
                },
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit record for event {EventType}", eventType);
        }
    }
}

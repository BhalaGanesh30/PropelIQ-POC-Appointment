using Microsoft.Extensions.Logging;
using PropelIQ.Api.Sessions;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Api;

/// <summary>
/// Handles account lockout side-effects (us_018 AC-3):
///   1. Invalidates all active sessions for the locked user.
///   2. Revokes all refresh tokens.
///   3. Sends a lockout-notification email to the account owner.
///   4. Records the event in the audit log.
///
/// Injected as a scoped service into <see cref="Controllers.AuthController"/>.
/// </summary>
public sealed class AccountLockoutHandler
{
    private readonly ISessionService _sessions;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly INotificationSender _notifications;
    private readonly AppDbContext _db;
    private readonly ILogger<AccountLockoutHandler> _logger;

    public AccountLockoutHandler(
        ISessionService sessions,
        IRefreshTokenRepository refreshTokens,
        INotificationSender notifications,
        AppDbContext db,
        ILogger<AccountLockoutHandler> logger)
    {
        _sessions = sessions;
        _refreshTokens = refreshTokens;
        _notifications = notifications;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Called the first time a lockout is detected for <paramref name="user"/>.
    /// All active sessions and refresh tokens are invalidated and the user is
    /// notified by email (AC-3).
    /// </summary>
    public async Task HandleAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken ct)
    {
        // 1. Invalidate active sessions — pushes SignalR "SessionEnded" to any open tab.
        await _sessions.InvalidateSessionAsync(user.Id, "AccountLockout", ct);

        // 2. Revoke all refresh tokens so no background process can silently re-auth.
        await _refreshTokens.RevokeAllForUserAsync(
            user.Id, "Account locked — 5 failed login attempts", ct);

        // 3. Notify the account owner via email (AC-3).
        try
        {
            await _notifications.SendEmailAsync(
                user.Email!,
                "Security Alert — Account Locked",
                $"Your PropelIQ account has been temporarily locked after 5 consecutive " +
                $"failed login attempts.\n\n" +
                $"Your account will unlock automatically in 30 minutes.\n\n" +
                $"If you did not make these attempts, please reset your password immediately " +
                $"after the lockout period expires.\n\n" +
                $"IP Address: {ipAddress ?? "Unknown"}\n" +
                $"Time: {DateTimeOffset.UtcNow:u}",
                ct);
        }
        catch (Exception ex)
        {
            // Notification failure must not block the lockout response.
            _logger.LogError(ex, "Failed to send lockout email to {Email}", user.Email);
        }

        // 4. Audit record — NFR-010.
        await WriteAuditAsync(
            user.Id,
            "auth.account_locked",
            $"Account locked after 5 failed attempts. Sessions invalidated. IP: {ipAddress ?? "Unknown"}",
            ipAddress,
            ct);
    }

    private async Task WriteAuditAsync(
        Guid userId,
        string eventType,
        string description,
        string? ipAddress,
        CancellationToken ct)
    {
        try
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                EventType = eventType,
                ActorUserId = userId,
                TargetEntityId = userId,
                TargetEntityType = nameof(ApplicationUser),
                OccurredAt = DateTimeOffset.UtcNow,
                Details = new AuditDetails
                {
                    IpAddress = ipAddress,
                    ChangeDescription = description
                }
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit record for event {EventType}", eventType);
        }
    }
}

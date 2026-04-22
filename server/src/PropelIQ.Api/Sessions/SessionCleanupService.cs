using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;

namespace PropelIQ.Api.Sessions;

/// <summary>
/// Background hosted service that purges expired sessions every 5 minutes.
/// Handles the browser-crash edge case: sessions with LastActivityAt older than
/// 15 minutes are terminated and their refresh tokens revoked (NFR-010).
/// Uses a per-cycle <see cref="IServiceScope"/> to avoid DbContext lifetime issues.
/// </summary>
public sealed class SessionCleanupService : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessionsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(CleanupIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down — exit the loop cleanly without crashing.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup cycle. Will retry in {Minutes} minutes.", CleanupIntervalMinutes);
                // Wait before retrying so a transient DB error doesn't spin the loop.
                try { await Task.Delay(TimeSpan.FromMinutes(CleanupIntervalMinutes), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IActiveSessionRepository>();
        var refreshTokenRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddMinutes(-SessionTimeoutMinutes);
        var expired = await sessionRepo.GetExpiredSessionsAsync(cutoff, ct);

        foreach (var session in expired)
        {
            session.IsActive = false;
            session.TerminatedAt = DateTime.UtcNow;
            session.TerminationReason = "InactivityTimeout";
            await sessionRepo.UpdateAsync(session, ct);

            await refreshTokenRepo.RevokeAllForUserAsync(
                session.UserId, "Session expired — inactivity", ct);

            try
            {
                db.AuditRecords.Add(new AuditRecord
                {
                    EventType = "session.expired",
                    ActorUserId = session.UserId,
                    TargetEntityId = session.UserId,
                    TargetEntityType = "ActiveSession",
                    OccurredAt = DateTimeOffset.UtcNow,
                    Details = new AuditDetails
                    {
                        ChangeDescription = "Session terminated due to inactivity timeout",
                    },
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to write audit record for expired session {SessionId}",
                    session.Id);
            }

            _logger.LogInformation(
                "Expired session {SessionId} for user {UserId} terminated.",
                session.Id, session.UserId);
        }

        if (expired.Count > 0)
            _logger.LogInformation("Cleaned up {Count} expired session(s).", expired.Count);
    }
}

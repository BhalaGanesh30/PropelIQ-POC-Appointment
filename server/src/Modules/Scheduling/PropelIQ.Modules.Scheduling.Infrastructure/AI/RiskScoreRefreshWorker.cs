using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.AI;
using PropelIQ.Modules.Scheduling.Application.AI.Models;

namespace PropelIQ.Modules.Scheduling.Infrastructure.AI;

/// <summary>
/// Background worker that pre-computes no-show risk scores for upcoming
/// appointments every 30 minutes.
///
/// Reduces inline scoring latency on the dashboard by ensuring scores for
/// appointments in the next 7 days are warm and within the 24-hour TTL (AC-4).
/// Any appointment that is stale or unscored is batched (up to 20 at a time)
/// and scored via <see cref="INoShowRiskScoringService.ScoreAsync"/>.
///
/// Errors on individual appointments are logged and swallowed — a single
/// scoring failure must not stop the refresh cycle.
/// </summary>
public sealed class RiskScoreRefreshWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(30);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RiskScoreRefreshWorker> _logger;

    public RiskScoreRefreshWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<RiskScoreRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RiskScoreRefreshWorker started.");

        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await RefreshStaleScoresAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Risk score refresh tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        _logger.LogInformation("RiskScoreRefreshWorker stopped.");
    }

    private async Task RefreshStaleScoresAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var scorer = scope.ServiceProvider.GetRequiredService<INoShowRiskScoringService>();

        var now = _timeProvider.GetUtcNow();
        var staleThreshold = now - NoShowRiskDefaults.StalenessThreshold;

        var staleIds = await repo.GetAppointmentsNeedingRiskScoreAsync(
            from: now,
            to: now.AddDays(7),
            staleThreshold: staleThreshold,
            limit: BatchSize,
            ct: ct);

        if (staleIds.Count == 0)
            return;

        _logger.LogInformation(
            "Refreshing {Count} stale risk scores in next 7 days",
            staleIds.Count);

        foreach (var id in staleIds)
        {
            try
            {
                await scorer.ScoreAsync(id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to refresh risk score for appointment {Id}", id);
            }
        }
    }
}

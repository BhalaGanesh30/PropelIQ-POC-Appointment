using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Intake;

/// <summary>
/// Background service that periodically marks expired intake drafts as Expired.
/// Runs every 6 hours — satisfies the 7-day draft retention policy (edge case: session expiry).
/// Uses IServiceScopeFactory to resolve scoped IIntakeDraftRepository from a singleton host.
/// </summary>
public sealed class IntakeDraftCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntakeDraftCleanupService> _logger;

    public IntakeDraftCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<IntakeDraftCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Intake draft cleanup service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider
                .GetRequiredService<IIntakeDraftRepository>();

            var count = await repo.ExpireOldDraftsAsync(ct);

            if (count > 0)
                _logger.LogInformation(
                    "Intake draft cleanup: expired {Count} draft(s).", count);
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down — expected.
        }
        catch (Exception ex)
        {
            // Log but do not crash the host — cleanup will retry on next interval.
            _logger.LogError(ex, "Intake draft cleanup failed.");
        }
    }
}

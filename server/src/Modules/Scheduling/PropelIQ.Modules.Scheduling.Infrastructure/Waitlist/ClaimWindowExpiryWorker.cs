using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

/// <summary>
/// Background worker that polls for Offered waitlist entries whose 2-hour claim
/// window has expired, marks them Expired, and rotates the slot to the next
/// eligible patient (AC-4).
///
/// Polling interval: 1 minute — balances expiry latency against DB load.
/// Design: BackgroundService is singleton; scoped deps resolved via IServiceScopeFactory.
/// </summary>
public sealed class ClaimWindowExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClaimWindowExpiryWorker> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public ClaimWindowExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ClaimWindowExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("ClaimWindowExpiryWorker started.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredOffersAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during claim window expiry check.");
            }

            await Task.Delay(CheckInterval, ct);
        }

        _logger.LogInformation("ClaimWindowExpiryWorker stopped.");
    }

    private async Task ProcessExpiredOffersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWaitlistRepository>();
        var svc  = scope.ServiceProvider.GetRequiredService<WaitlistService>();

        var expired = await repo.GetExpiredOffersAsync(ct);

        foreach (var entry in expired)
        {
            try
            {
                await svc.ExpireAndRotateAsync(entry, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to expire waitlist entry {EntryId}.", entry.Id);
            }
        }

        if (expired.Count > 0)
        {
            _logger.LogInformation(
                "Expired {Count} waitlist offer(s) and rotated to next eligible patients.",
                expired.Count);
        }
    }
}

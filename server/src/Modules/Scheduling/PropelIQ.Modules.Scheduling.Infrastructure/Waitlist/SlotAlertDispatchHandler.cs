using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Waitlist;
using PropelIQ.Modules.Scheduling.Domain.Events;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

/// <summary>
/// Hosted service that drains the <see cref="Channel{T}"/> of
/// <see cref="SlotOfferedEvent"/> and delegates dispatch to
/// <see cref="ISlotAlertService"/> (email + SMS within 5 minutes — AC-1).
///
/// Pattern: singleton <see cref="Channel{SlotOfferedEvent}"/> injected from DI
/// (already registered by US_023 in <c>SchedulingServiceRegistration</c>);
/// <see cref="IServiceScopeFactory"/> used to resolve scoped
/// <see cref="ISlotAlertService"/> + <see cref="AppDbContext"/> per message
/// (mirrors <c>WaitlistMatchingWorker</c>, <c>RiskScoreRefreshWorker</c>).
/// </summary>
public sealed class SlotAlertDispatchHandler : BackgroundService
{
    private readonly Channel<SlotOfferedEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlotAlertDispatchHandler> _logger;

    public SlotAlertDispatchHandler(
        Channel<SlotOfferedEvent> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<SlotAlertDispatchHandler> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SlotAlertDispatchHandler started — awaiting slot-offered events.");

        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessEventAsync(evt, stoppingToken);
        }

        _logger.LogInformation("SlotAlertDispatchHandler stopped.");
    }

    private async Task ProcessEventAsync(SlotOfferedEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertService = scope.ServiceProvider.GetRequiredService<ISlotAlertService>();

        try
        {
            await alertService.DispatchAlertAsync(evt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down — do not log as error.
            throw;
        }
        catch (Exception ex)
        {
            // Log and continue — a failed alert must not crash the dispatch loop
            // or block the channel for subsequent entries (AC-1 SLA preservation).
            _logger.LogError(
                ex,
                "SlotAlertDispatchHandler: unhandled exception dispatching alert " +
                "for waitlist entry {WaitlistEntryId}. Event will not be retried.",
                evt.WaitlistEntryId);
        }
    }
}

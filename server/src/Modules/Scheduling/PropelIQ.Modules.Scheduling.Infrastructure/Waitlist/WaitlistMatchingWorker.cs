using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Waitlist;

/// <summary>
/// Message written to the in-process channel when a slot is released
/// (cancellation from US_022, reschedule slot swap, or expiry rotation).
/// </summary>
public sealed record SlotReleasedMessage
{
    public Guid SlotId { get; init; }
    public DateTimeOffset SlotTime { get; init; }
    public int DurationMinutes { get; init; }
    public string AppointmentType { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
}

/// <summary>
/// Background worker that consumes <see cref="SlotReleasedMessage"/> from the
/// in-process channel and matches the released slot to the first FIFO-eligible
/// waitlisted patient (AC-2).
///
/// The channel is written by the cancellation/reschedule handlers (US_022) so the
/// maximum latency from slot availability to notification is bounded by the channel
/// consumer loop (target: within 5 minutes per AC-2).
///
/// Design: BackgroundService is singleton; scoped deps are resolved per message
/// via IServiceScopeFactory to avoid captive dependency lifetime violations.
/// </summary>
public sealed class WaitlistMatchingWorker : BackgroundService
{
    private readonly Channel<SlotReleasedMessage> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistMatchingWorker> _logger;

    public WaitlistMatchingWorker(
        Channel<SlotReleasedMessage> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistMatchingWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("WaitlistMatchingWorker started.");

        await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<WaitlistService>();

                await svc.MatchSlotToWaitlistAsync(
                    msg.SlotId,
                    msg.SlotTime,
                    msg.DurationMinutes,
                    msg.AppointmentType,
                    msg.ProviderName,
                    ct);
            }
            catch (Exception ex)
            {
                // Log and continue — a single failed match must not halt the worker.
                _logger.LogError(ex,
                    "Failed to match released slot {SlotId} to waitlist.", msg.SlotId);
            }
        }

        _logger.LogInformation("WaitlistMatchingWorker stopped.");
    }
}

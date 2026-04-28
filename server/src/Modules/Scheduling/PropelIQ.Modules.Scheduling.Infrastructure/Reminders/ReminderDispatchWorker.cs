using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Reminders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Reminders;

/// <summary>
/// Background worker that polls the <c>ReminderEvent</c> table once per minute
/// and dispatches due reminders via the configured notification channels.
///
/// AC-2: Picks up reminders within a 1-minute tolerance window of their
///       <c>ScheduledAt</c> time on each tick.
/// Edge case 1: All state is in the database — on restart the worker
///              immediately resumes from the next pending event.
/// Edge case 2: <c>TryClaimForDispatchAsync</c> transitions Pending→Sending
///              atomically so concurrent worker instances cannot double-send.
///
/// Retry policy: the worker itself retries up to <see cref="MaxRetries"/> times
/// (incrementing <c>RetryCount</c>) before transitioning to <c>Failed</c>.
/// The inner Polly pipeline inside <see cref="NotificationDispatcher"/> adds
/// 2 automatic transient retries per attempt.
///
/// NFR-010: Structured logging for every dispatch success, failure, and batch.
/// </summary>
public sealed class ReminderDispatchWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Tolerance     = TimeSpan.FromMinutes(1);
    private const int BatchSize  = 50;
    private const int MaxRetries = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderDispatchWorker> _logger;

    public ReminderDispatchWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<ReminderDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger       = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ReminderDispatchWorker started. Poll interval: {Interval}.",
            PollInterval);

        using var timer = new PeriodicTimer(PollInterval);

        // Edge case 1: Process immediately on start so overdue reminders are not
        // delayed until the first tick fires.
        do
        {
            try
            {
                await ProcessDueBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during graceful shutdown — exit the loop cleanly.
                break;
            }
            catch (Exception ex)
            {
                // Catch-all: a broken tick must not crash the worker.
                // NFR-010: Log for operational alerting.
                _logger.LogError(ex, "ReminderDispatchWorker tick failed unexpectedly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        _logger.LogInformation("ReminderDispatchWorker stopped.");
    }

    private async Task ProcessDueBatchAsync(CancellationToken ct)
    {
        using var scope    = _scopeFactory.CreateScope();
        var repo           = scope.ServiceProvider.GetRequiredService<IReminderDispatchRepository>();
        var dispatcher     = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var now = _timeProvider.GetUtcNow();
        var due = await repo.GetDueRemindersAsync(now, Tolerance, BatchSize, ct);

        if (due.Count == 0)
            return;

        _logger.LogInformation(
            "ReminderDispatchWorker: processing {Count} due reminder(s) at {Now}.",
            due.Count, now);

        foreach (var reminder in due)
        {
            // Edge case 2: Claim atomically; skip if already taken by another instance.
            var claimed = await repo.TryClaimForDispatchAsync(reminder.Id, ct);
            if (!claimed)
            {
                _logger.LogDebug(
                    "Reminder {ReminderId} already claimed by another worker; skipping.",
                    reminder.Id);
                continue;
            }

            try
            {
                await dispatcher.DispatchAsync(reminder, ct);

                await repo.MarkSentAsync(reminder.Id, _timeProvider.GetUtcNow(), ct);

                _logger.LogInformation(
                    "Reminder {ReminderId} dispatched via {Channel} for appointment {AppointmentId}.",
                    reminder.Id, reminder.Channel, reminder.AppointmentId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutdown mid-dispatch — reset to Pending so the next worker picks it up.
                await repo.MarkRetryOrFailedAsync(reminder.Id, MaxRetries, ct);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Dispatch failed for reminder {ReminderId} (retry {Retry}/{Max}); " +
                    "resetting to Pending or marking Failed.",
                    reminder.Id, reminder.RetryCount + 1, MaxRetries);

                await repo.MarkRetryOrFailedAsync(reminder.Id, MaxRetries, ct);

                // AC-4: Persist dead-letter when all retries are exhausted.
                if (reminder.RetryCount + 1 >= MaxRetries)
                {
                    try
                    {
                        var deadLetterRepo = scope.ServiceProvider
                            .GetRequiredService<IDeadLetterRepository>();

                        await deadLetterRepo.AddAsync(new DeadLetterEvent
                        {
                            AppointmentId    = reminder.AppointmentId,
                            SourceReminderId = reminder.Id,
                            Channel          = reminder.Channel,
                            FailureReason    = ex.Message,
                            TotalAttempts    = MaxRetries,
                            Reprocessed      = false
                        }, ct);

                        _logger.LogWarning(
                            "Reminder {ReminderId} moved to dead-letter after {MaxRetries} failed attempts.",
                            reminder.Id, MaxRetries);
                    }
                    catch (Exception dlEx)
                    {
                        // Dead-letter persistence failure must not mask the original error.
                        _logger.LogError(
                            dlEx,
                            "Failed to persist dead-letter for reminder {ReminderId}.",
                            reminder.Id);
                    }
                }
            }
        }
    }
}

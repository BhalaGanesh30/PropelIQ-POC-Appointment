using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.AI.Models;

namespace PropelIQ.Modules.Scheduling.Infrastructure.AI;

/// <summary>
/// Background worker that polls every hour for High-risk appointments scheduled
/// within the next 24 hours (±30-minute window) and publishes a
/// <see cref="HighRiskAlertEvent"/> to the in-process channel for each one.
///
/// AC-3: Staff are notified 24 hours before a High-risk appointment so they can
/// consider manual follow-up before the patient is due to arrive.
///
/// The channel is consumed by notification infrastructure (SignalR push,
/// email/SMS dispatch) to surface the alert to the assigned staff member.
/// </summary>
public sealed class HighRiskNotificationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan AlertWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan WindowTolerance = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelWriter<HighRiskAlertEvent> _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HighRiskNotificationWorker> _logger;

    public HighRiskNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ChannelWriter<HighRiskAlertEvent> writer,
        TimeProvider timeProvider,
        ILogger<HighRiskNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _writer = writer;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HighRiskNotificationWorker started.");

        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await CheckHighRiskAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "High-risk notification tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        _logger.LogInformation("HighRiskNotificationWorker stopped.");
    }

    // AC-3: Surface staff follow-up prompt for High-risk appointments 24h out.
    private async Task CheckHighRiskAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        var now = _timeProvider.GetUtcNow();
        var windowStart = now + AlertWindow - WindowTolerance;
        var windowEnd   = now + AlertWindow + WindowTolerance;

        var highRiskAppointments = await repo
            .GetHighRiskAppointmentsInWindowAsync(windowStart, windowEnd, ct);

        foreach (var appt in highRiskAppointments)
        {
            var evt = new HighRiskAlertEvent(
                AppointmentId:   appt.Id,
                PatientName:     appt.PatientName,
                AppointmentDate: appt.ScheduledAt,
                RiskLevel:       appt.RiskLevel ?? "High",
                Confidence:      appt.RiskConfidence ?? 0.0);

            await _writer.WriteAsync(evt, ct);

            _logger.LogInformation(
                "High-risk alert published for appointment {Id} at {Date}",
                appt.Id, appt.ScheduledAt);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Compliance;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Polls active compliance report schedules every minute and triggers
/// report generation for any schedule whose <c>NextRunAt</c> is overdue (US_058, AC-5).
///
/// Pattern follows <see cref="RetentionPolicyWorker"/>:
/// <list type="bullet">
///   <item><see cref="IServiceScopeFactory"/> used to resolve the scoped <see cref="AppDbContext"/>.</item>
///   <item><see cref="PeriodicTimer"/> with 1-minute period; no Thread.Sleep or Task.Delay.</item>
///   <item>Recurrence projection updated after each run: Daily +1 day, Weekly +7 days, Monthly +1 month.</item>
/// </list>
/// </summary>
public sealed class ComplianceReportScheduleWorker : BackgroundService
{
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly ILogger<ComplianceReportScheduleWorker> _logger;

    public ComplianceReportScheduleWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ComplianceReportScheduleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ComplianceReportScheduleWorker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing compliance report schedules.");
            }
        }

        _logger.LogInformation("ComplianceReportScheduleWorker stopped.");
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken ct)
    {
        await using var scope   = _scopeFactory.CreateAsyncScope();
        var db                  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reportService       = scope.ServiceProvider.GetRequiredService<IComplianceReportService>();

        var now = DateTime.UtcNow;

        var dueSchedules = await db.ComplianceReportSchedules
            .Where(s => s.IsActive && s.NextRunAt <= now)
            .ToListAsync(ct);

        if (dueSchedules.Count == 0) return;

        _logger.LogInformation(
            "Processing {Count} due compliance report schedule(s).", dueSchedules.Count);

        foreach (var schedule in dueSchedules)
        {
            try
            {
                // Calculate report period from recurrence.
                var (periodStart, periodEnd) = CalculatePeriod(schedule.Recurrence, now);

                var request = new ReportRequest
                {
                    ReportType        = schedule.ReportType,
                    PeriodStartUtc    = periodStart,
                    PeriodEndUtc      = periodEnd,
                    RequestedByUserId = null, // system-generated
                };

                await reportService.GenerateAsync(request, ct);

                // Advance the schedule.
                schedule.LastRunAt = now;
                schedule.NextRunAt = AdvanceNextRun(schedule.Recurrence, now);

                _logger.LogInformation(
                    "Schedule '{Name}' ({Recurrence}) triggered report. NextRunAt = {Next}.",
                    schedule.Name, schedule.Recurrence, schedule.NextRunAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to execute schedule '{Name}' ({ScheduleId}).",
                    schedule.Name, schedule.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Recurrence helpers ────────────────────────────────────────────────────

    private static (DateTime Start, DateTime End) CalculatePeriod(string recurrence, DateTime now)
    {
        return recurrence switch
        {
            "Daily"   => (now.AddDays(-1).Date, now.Date.AddSeconds(-1)),
            "Weekly"  => (now.AddDays(-7).Date, now.Date.AddSeconds(-1)),
            "Monthly" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1).AddSeconds(-1)),
            _         => (now.AddDays(-1).Date, now.Date.AddSeconds(-1)),
        };
    }

    private static DateTime AdvanceNextRun(string recurrence, DateTime from)
    {
        return recurrence switch
        {
            "Daily"   => from.AddDays(1),
            "Weekly"  => from.AddDays(7),
            "Monthly" => from.AddMonths(1),
            _         => from.AddDays(1),
        };
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using PropelIQ.Modules.SharedServices.Application.Configuration;
using PropelIQ.Modules.SharedServices.Application.Kpi;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Kpi;

/// <summary>
/// Scheduled background worker that generates and emails a KPI PDF report to configured
/// recipients (US_060, AC-4).
///
/// <para>
/// Schedule: The worker ticks every 5 minutes. On each tick it checks whether:
/// <list type="number">
///   <item>The <c>CommunicationTemplates</c> configuration contains a
///         <c>kpiDistributionRecipients</c> value (comma-separated emails).</item>
///   <item>Today is Monday and the current UTC hour is 8 (weekly at 08:00 UTC).</item>
///   <item>A <c>Sent</c> distribution for last week's period has not already been logged.</item>
/// </list>
/// When all three conditions are met, the worker generates last week's KPI PDF and delivers
/// it via <see cref="INotificationSender.SendEmailAsync"/> with Polly retry (3 attempts,
/// exponential back-off starting at 2 s).
/// </para>
///
/// <para>
/// Attachment note: <see cref="INotificationSender.SendEmailAsync"/> does not natively support
/// MIME multipart attachments. The PDF file name is referenced in the HTML body and is available
/// for download via the admin dashboard export endpoint. Replace with a multipart-capable sender
/// (e.g. MailKit, SendGrid) to add true attachment support.
/// </para>
///
/// Registered as a <see cref="BackgroundService"/> in <c>SharedServicesServiceRegistration</c>.
/// Uses <see cref="IServiceScopeFactory"/> to resolve scoped services per tick.
/// </summary>
public sealed class KpiDistributionWorker : BackgroundService
{
    // Polly: 3 retries with exponential back-off (2 s, 4 s, 8 s).
    private static readonly ResiliencePipeline _retryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(2),
                BackoffType      = DelayBackoffType.Exponential,
                ShouldHandle     = new PredicateBuilder().Handle<Exception>(),
            })
            .Build();

    private readonly IServiceScopeFactory                _scopeFactory;
    private readonly ILogger<KpiDistributionWorker>      _logger;

    public KpiDistributionWorker(
        IServiceScopeFactory               scopeFactory,
        ILogger<KpiDistributionWorker>     logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KpiDistributionWorker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TryDistributeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "KpiDistributionWorker tick failed.");
            }
        }

        _logger.LogInformation("KpiDistributionWorker stopped.");
    }

    // ── Core distribution logic ───────────────────────────────────────────────

    private async Task TryDistributeAsync(CancellationToken ct)
    {
        await using var scope  = _scopeFactory.CreateAsyncScope();
        var config         = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
        var metrics        = scope.ServiceProvider.GetRequiredService<IKpiMetricsService>();
        var notifications  = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        var db             = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Read distribution recipients from CommunicationTemplates config.
        ConfigurationSnapshot configSnapshot;
        try
        {
            configSnapshot = await config.GetCurrentAsync(
                ConfigurationCategory.CommunicationTemplates, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot read CommunicationTemplates configuration — skipping KPI distribution tick.");
            return;
        }

        var recipientsValue = configSnapshot.Values.GetValueOrDefault("kpiDistributionRecipients");
        if (recipientsValue is null) return;

        var recipients = recipientsValue.ToString();
        if (string.IsNullOrWhiteSpace(recipients)) return;

        // 2. Check schedule: weekly on Monday at 08:xx UTC.
        var now = DateTime.UtcNow;
        if (now.DayOfWeek != DayOfWeek.Monday || now.Hour != 8) return;

        // 3. Compute this week's Monday and last week's report period.
        var daysSinceMonday = (int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1;
        var thisWeekMonday  = DateOnly.FromDateTime(now.AddDays(-daysSinceMonday));
        var periodFrom      = thisWeekMonday.AddDays(-7);   // last Monday
        var periodTo        = thisWeekMonday.AddDays(-1);   // last Sunday

        // 4. Guard: skip if already successfully distributed for this period.
        var alreadySent = await db.KpiDistributionLogs
            .AnyAsync(l => l.PeriodFrom == periodFrom && l.Status == "Sent", ct);

        if (alreadySent)
        {
            _logger.LogDebug(
                "KPI report for {PeriodFrom} already distributed. Skipping.", periodFrom);
            return;
        }

        _logger.LogInformation(
            "Generating KPI report for period {PeriodFrom} to {PeriodTo}.", periodFrom, periodTo);

        // 5. Generate PDF export.
        var range  = new DateRange(periodFrom, periodTo);
        var export = await metrics.ExportAsync(new KpiExportRequest(range, KpiExportFormat.Pdf), ct);

        // 6. Deliver to each recipient with Polly retry.
        string? errorDetail = null;
        try
        {
            await _retryPipeline.ExecuteAsync(async innerCt =>
            {
                var subject = $"[PropelIQ] Weekly KPI Report — {periodFrom:yyyy-MM-dd} to {periodTo:yyyy-MM-dd}";
                var body    = BuildEmailBody(periodFrom, periodTo, export.FileName);

                foreach (var email in recipients.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    await notifications.SendEmailAsync(email, subject, body, innerCt);
                    _logger.LogInformation(
                        "KPI report {FileName} dispatched to {Email}.", export.FileName, email);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            errorDetail = ex.Message;
            _logger.LogError(
                ex,
                "Failed to deliver KPI report for {PeriodFrom} to {PeriodTo} after retries.",
                periodFrom, periodTo);
        }

        // 7. Append distribution log entry.
        db.KpiDistributionLogs.Add(new KpiDistributionLog
        {
            PeriodFrom      = periodFrom,
            PeriodTo        = periodTo,
            RecipientEmails = recipients,
            Status          = errorDetail is null ? "Sent" : "Failed",
            SentAtUtc       = DateTime.UtcNow,
            ErrorDetail     = errorDetail,
        });

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildEmailBody(DateOnly from, DateOnly to, string fileName) =>
        $"<p>Your weekly KPI report for the period <strong>{from:yyyy-MM-dd}</strong> to " +
        $"<strong>{to:yyyy-MM-dd}</strong> is ready.</p>" +
        $"<p>File: <code>{fileName}</code></p>" +
        "<p>Download the full report from the <a href=\"/admin/kpi\">PropelIQ admin dashboard</a>.</p>" +
        "<hr/><p style=\"color:#666;font-size:0.85em\">PropelIQ Healthcare Platform — " +
        "This is an automated message.</p>";
}

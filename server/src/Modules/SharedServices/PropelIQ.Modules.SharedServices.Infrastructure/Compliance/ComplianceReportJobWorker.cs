using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Drains <see cref="ComplianceJobChannel"/> and executes async compliance report jobs
/// (US_058, edge case 1 — date ranges &gt; 90 days).
///
/// Pattern mirrors <see cref="AuditRecordWriterWorker"/>:
/// <list type="bullet">
///   <item><c>await foreach</c> over the channel reader (blocks until items are available).</item>
///   <item><see cref="IServiceScopeFactory"/> used to resolve scoped services per job.</item>
///   <item>On <see cref="DbUpdateException"/> or unhandled exceptions the job record is
///       marked "Failed" and the worker continues — a single bad job never crashes the host.</item>
/// </list>
/// </summary>
public sealed class ComplianceReportJobWorker : BackgroundService
{
    private readonly ComplianceJobChannel               _channel;
    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly ILogger<ComplianceReportJobWorker> _logger;

    public ComplianceReportJobWorker(
        ComplianceJobChannel                channel,
        IServiceScopeFactory                scopeFactory,
        ILogger<ComplianceReportJobWorker>  logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ComplianceReportJobWorker started.");

        await foreach (var jobId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error processing compliance job {JobId}.", jobId);
                await TryMarkJobFailedAsync(jobId, ex.Message, stoppingToken);
            }
        }

        _logger.LogInformation("ComplianceReportJobWorker stopped.");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        await using var scope  = _scopeFactory.CreateAsyncScope();
        var db                 = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var generator          = scope.ServiceProvider.GetRequiredService<ComplianceReportGenerator>();
        var renderer           = scope.ServiceProvider.GetRequiredService<ComplianceReportPdfRenderer>();
        var distributor        = scope.ServiceProvider.GetRequiredService<ComplianceReportDistributor>();

        var job = await db.ComplianceReportJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
        {
            _logger.LogWarning("Compliance job {JobId} not found in database — skipping.", jobId);
            return;
        }

        var report = await db.ComplianceReports.FirstOrDefaultAsync(r => r.Id == job.ReportId, ct);
        if (report is null)
        {
            _logger.LogWarning(
                "Compliance report {ReportId} for job {JobId} not found — skipping.",
                job.ReportId, jobId);
            return;
        }

        _logger.LogInformation(
            "Processing async compliance job {JobId} for report {ReportId}.", jobId, job.ReportId);

        // Mark in-progress.
        job.Status = "Generating";
        await db.SaveChangesAsync(ct);

        try
        {
            var request = System.Text.Json.JsonSerializer.Deserialize<
                PropelIQ.Modules.SharedServices.Application.Compliance.ReportRequest>(job.RequestJson)!;

            var data     = await generator.GenerateAsync(request, job.ReportId, ct);
            var pdfBytes = renderer.Render(data);

            // Update report record with results.
            report.TotalAuditEvents      = data.KeyMetrics.TotalAuditEvents;
            report.UniqueActors          = data.KeyMetrics.UniqueActors;
            report.AnomalyCount          = data.KeyMetrics.AnomalyCount;
            report.FailedAccessAttempts  = data.KeyMetrics.FailedAccessAttempts;
            report.PdfContent            = pdfBytes;
            report.Status                = "Completed";
            report.GeneratedAtUtc        = data.GeneratedAtUtc;

            job.Status         = "Completed";
            job.CompletedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Async compliance report {ReportId} completed (job {JobId}).",
                job.ReportId, jobId);

            // Distribute PDF to distribution list.
            await distributor.DistributeAsync(
                report.Id,
                pdfBytes,
                report.ReportType,
                report.PeriodStartUtc,
                report.PeriodEndUtc,
                _scopeFactory,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate async compliance report {ReportId} (job {JobId}).",
                job.ReportId, jobId);

            job.Status       = "Failed";
            job.ErrorMessage = ex.Message;
            report.Status    = "Failed";

            await db.SaveChangesAsync(ct);
            throw; // Let outer catch log it.
        }
    }

    private async Task TryMarkJobFailedAsync(
        Guid              jobId,
        string            errorMessage,
        CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var job = await db.ComplianceReportJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job is null) return;

            job.Status       = "Failed";
            job.ErrorMessage = errorMessage;

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update job {JobId} status to Failed after unhandled error.", jobId);
        }
    }
}

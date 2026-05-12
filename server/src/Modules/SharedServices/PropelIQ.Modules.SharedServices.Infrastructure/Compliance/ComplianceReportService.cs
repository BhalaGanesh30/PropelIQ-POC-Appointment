using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Compliance;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Implements <see cref="IComplianceReportService"/> — the main orchestration layer for
/// compliance report generation, storage, retrieval, and download (US_058, AC-1–AC-4).
///
/// <para>
/// Synchronous path (≤ 90-day range): generate → render PDF → persist → return Completed.
/// Async path (&gt; 90-day range): pre-allocate report row with status "Generating",
/// enqueue job to <see cref="ComplianceJobChannel"/>, return IsAsync = true (edge case 1).
/// </para>
/// </summary>
public sealed class ComplianceReportService : IComplianceReportService
{
    private readonly AppDbContext                   _db;
    private readonly ComplianceReportGenerator      _generator;
    private readonly ComplianceReportPdfRenderer    _renderer;
    private readonly ComplianceJobChannel           _jobChannel;
    private readonly ILogger<ComplianceReportService> _logger;

    public ComplianceReportService(
        AppDbContext                        db,
        ComplianceReportGenerator           generator,
        ComplianceReportPdfRenderer         renderer,
        ComplianceJobChannel                jobChannel,
        ILogger<ComplianceReportService>    logger)
    {
        _db         = db;
        _generator  = generator;
        _renderer   = renderer;
        _jobChannel = jobChannel;
        _logger     = logger;
    }

    /// <inheritdoc />
    public async Task<ReportGenerationResult> GenerateAsync(
        ReportRequest       request,
        CancellationToken   ct = default)
    {
        var span = request.PeriodEndUtc - request.PeriodStartUtc;

        // ── Async path: range > 90 days ───────────────────────────────────────
        if (span > ComplianceReportGenerator.AsyncThreshold)
        {
            return await EnqueueAsyncJobAsync(request, ct);
        }

        // ── Sync path ─────────────────────────────────────────────────────────
        // Pre-create the record so BaseEntity auto-generates the Id; read it back to
        // pass into the generator (used as ComplianceReportData.ReportId).
        var record = new ComplianceReportRecord
        {
            ReportType     = request.ReportType,
            PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc   = request.PeriodEndUtc,
            GeneratedAtUtc = DateTime.UtcNow,
            Status         = "Generating",
            IsAsync        = false,
        };

        var data     = await _generator.GenerateAsync(request, record.Id, ct);
        var pdfBytes = _renderer.Render(data);

        // Update the pre-allocated record with generated results.
        record.TotalAuditEvents      = data.KeyMetrics.TotalAuditEvents;
        record.UniqueActors          = data.KeyMetrics.UniqueActors;
        record.AnomalyCount          = data.KeyMetrics.AnomalyCount;
        record.FailedAccessAttempts  = data.KeyMetrics.FailedAccessAttempts;
        record.PdfContent            = pdfBytes;
        record.Status                = "Completed";
        record.GeneratedAtUtc        = data.GeneratedAtUtc;

        _db.ComplianceReports.Add(record);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Compliance report {ReportId} generated synchronously ({Events} events, {Anomalies} anomalies).",
            record.Id, data.KeyMetrics.TotalAuditEvents, data.KeyMetrics.AnomalyCount);

        return new ReportGenerationResult
        {
            Id      = record.Id,
            IsAsync = false,
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReportSummary>> ListAsync(
        int               page,
        int               pageSize,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var total = await _db.ComplianceReports.CountAsync(ct);

        var items = await _db.ComplianceReports
            .AsNoTracking()
            .OrderByDescending(r => r.GeneratedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReportSummary
            {
                Id              = r.Id,
                ReportType      = r.ReportType,
                PeriodStartUtc  = r.PeriodStartUtc,
                PeriodEndUtc    = r.PeriodEndUtc,
                GeneratedAtUtc  = r.GeneratedAtUtc,
                TotalAuditEvents = r.TotalAuditEvents,
                UniqueActors    = r.UniqueActors,
                AnomalyCount    = r.AnomalyCount,
                IsAsync         = r.IsAsync,
                Status          = r.Status,
            })
            .ToListAsync(ct);

        return new PagedResult<ReportSummary>
        {
            Items    = items,
            Total    = total,
            Page     = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public async Task<ReportSummary?> GetAsync(
        Guid              reportId,
        CancellationToken ct = default)
    {
        var r = await _db.ComplianceReports
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reportId, ct);

        if (r is null) return null;

        return new ReportSummary
        {
            Id              = r.Id,
            ReportType      = r.ReportType,
            PeriodStartUtc  = r.PeriodStartUtc,
            PeriodEndUtc    = r.PeriodEndUtc,
            GeneratedAtUtc  = r.GeneratedAtUtc,
            TotalAuditEvents = r.TotalAuditEvents,
            UniqueActors    = r.UniqueActors,
            AnomalyCount    = r.AnomalyCount,
            IsAsync         = r.IsAsync,
            Status          = r.Status,
        };
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadPdfAsync(
        Guid              reportId,
        CancellationToken ct = default)
    {
        var record = await _db.ComplianceReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (record?.PdfContent is null) return null;

        // Return a MemoryStream — caller is responsible for disposing after streaming the response.
        return new MemoryStream(record.PdfContent);
    }

    /// <inheritdoc />
    public async Task<ReportJobStatus?> GetJobStatusAsync(
        Guid              jobId,
        CancellationToken ct = default)
    {
        var job = await _db.ComplianceReportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null) return null;

        return new ReportJobStatus
        {
            JobId        = job.Id,
            Status       = job.Status,
            ReportId     = job.ReportId,
            CompletedAt  = job.CompletedAtUtc,
            ErrorMessage = job.ErrorMessage,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<ReportGenerationResult> EnqueueAsyncJobAsync(
        ReportRequest     request,
        CancellationToken ct)
    {
        // Pre-allocate the report row — BaseEntity auto-generates Id.
        var record = new ComplianceReportRecord
        {
            ReportType     = request.ReportType,
            PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc   = request.PeriodEndUtc,
            GeneratedAtUtc = DateTime.UtcNow,
            Status         = "Generating",
            IsAsync        = true,
        };
        _db.ComplianceReports.Add(record);

        var job = new ComplianceReportJob
        {
            ReportId      = record.Id,
            RequestedBy   = request.RequestedByUserId ?? Guid.Empty,
            RequestJson   = JsonSerializer.Serialize(request),
            Status        = "Queued",
            CreatedAtUtc  = DateTime.UtcNow,
        };
        _db.ComplianceReportJobs.Add(job);

        await _db.SaveChangesAsync(ct);

        // Write to bounded channel — ComplianceReportJobWorker drains it.
        await _jobChannel.Writer.WriteAsync(job.Id, ct);

        _logger.LogInformation(
            "Compliance report {ReportId} queued as async job {JobId} (period > 90 days).",
            record.Id, job.Id);

        // Update report record to store the job ID link.
        record.JobId = job.Id;
        await _db.SaveChangesAsync(ct);

        return new ReportGenerationResult
        {
            Id      = record.Id,
            IsAsync = true,
            JobId   = job.Id,
        };
    }
}

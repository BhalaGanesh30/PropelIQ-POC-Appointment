using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Kpi;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Kpi;

/// <summary>
/// Aggregates KPI metrics from the <c>appointments</c> and <c>kpi_daily_metrics</c> tables
/// and exposes them via <see cref="IKpiMetricsService"/> (US_060, AC-1, AC-2, AC-3).
///
/// <para>
/// Summary results are cached in <see cref="KpiSnapshotCacheService"/> for 1 hour (edge case 1).
/// </para>
/// <para>
/// Empty date ranges (no appointments) return zero-value cards without errors (edge case 2).
/// </para>
///
/// Registered as scoped in <c>SharedServicesServiceRegistration</c> — receives a fresh
/// <see cref="AppDbContext"/> per request.
/// </para>
/// </summary>
public sealed class KpiMetricsService : IKpiMetricsService
{
    private readonly AppDbContext               _db;
    private readonly KpiSnapshotCacheService    _cache;
    private readonly KpiReportPdfRenderer       _renderer;
    private readonly ILogger<KpiMetricsService> _logger;

    public KpiMetricsService(
        AppDbContext               db,
        KpiSnapshotCacheService    cache,
        KpiReportPdfRenderer       renderer,
        ILogger<KpiMetricsService> logger)
    {
        _db       = db;
        _cache    = cache;
        _renderer = renderer;
        _logger   = logger;
    }

    /// <inheritdoc/>
    public async Task<KpiSummaryResponse> GetSummaryAsync(DateRange range, CancellationToken ct = default)
    {
        var cached = _cache.TryGet(range);
        if (cached is not null)
        {
            _logger.LogDebug("KPI summary cache hit for range {From}-{To}.", range.From, range.To);
            return cached;
        }

        var fromDt = range.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDt   = range.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        // ── Total and no-show count from Appointments ─────────────────────────
        var total = await _db.Appointments
            .Where(a => a.ScheduledAt >= fromDt && a.ScheduledAt <= toDt)
            .CountAsync(ct);

        var noShows = await _db.Appointments
            .Where(a => a.ScheduledAt >= fromDt
                     && a.ScheduledAt <= toDt
                     && a.Status == AppointmentStatus.NoShow.ToString())
            .CountAsync(ct);

        var noShowRate = total > 0
            ? Math.Round((decimal)noShows / total * 100, 2)
            : 0m;

        // ── Average wait time (minutes) — computed in-memory to stay portable ──
        // Npgsql does not provide a direct SQL DateDiff function; loading timestamps
        // for the wait-time subset is safe because it is bounded by the date range.
        var arrivals = await _db.Appointments
            .Where(a => a.ScheduledAt >= fromDt
                     && a.ScheduledAt <= toDt
                     && a.ArrivedAt != null)
            .Select(a => new { a.ScheduledAt, a.ArrivedAt })
            .ToListAsync(ct);

        var avgWaitMinutes = arrivals.Count > 0
            ? (decimal)arrivals.Average(a => (a.ArrivedAt!.Value - a.ScheduledAt).TotalMinutes)
            : 0m;
        avgWaitMinutes = Math.Round(avgWaitMinutes, 2);

        // ── Utilization from kpi_daily_metrics (pre-computed slot data) ────────
        var dailySlots = await _db.KpiDailyMetrics
            .Where(m => m.Date >= range.From && m.Date <= range.To)
            .Select(m => new { m.BookedSlots, m.AvailableSlots })
            .ToListAsync(ct);

        var totalBooked    = dailySlots.Sum(s => s.BookedSlots);
        var totalAvailable = dailySlots.Sum(s => s.AvailableSlots);
        var utilizationRate = totalAvailable > 0
            ? Math.Round((decimal)totalBooked / totalAvailable * 100, 2)
            : 0m;

        // ── Previous-period comparison ─────────────────────────────────────────
        var periodDays  = range.To.DayNumber - range.From.DayNumber + 1;
        var prevFrom    = range.From.AddDays(-periodDays);
        var prevTo      = range.From.AddDays(-1);
        var prevFromDt  = prevFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevToDt    = prevTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var prevTotal = await _db.Appointments
            .Where(a => a.ScheduledAt >= prevFromDt && a.ScheduledAt <= prevToDt)
            .CountAsync(ct);

        var prevNoShows = await _db.Appointments
            .Where(a => a.ScheduledAt >= prevFromDt
                     && a.ScheduledAt <= prevToDt
                     && a.Status == AppointmentStatus.NoShow.ToString())
            .CountAsync(ct);

        var prevNoShowRate = prevTotal > 0
            ? Math.Round((decimal)prevNoShows / prevTotal * 100, 2)
            : 0m;

        var prevArrivals = await _db.Appointments
            .Where(a => a.ScheduledAt >= prevFromDt
                     && a.ScheduledAt <= prevToDt
                     && a.ArrivedAt != null)
            .Select(a => new { a.ScheduledAt, a.ArrivedAt })
            .ToListAsync(ct);

        var prevAvgWait = prevArrivals.Count > 0
            ? (decimal)prevArrivals.Average(a => (a.ArrivedAt!.Value - a.ScheduledAt).TotalMinutes)
            : 0m;
        prevAvgWait = Math.Round(prevAvgWait, 2);

        var prevSlots = await _db.KpiDailyMetrics
            .Where(m => m.Date >= prevFrom && m.Date <= prevTo)
            .Select(m => new { m.BookedSlots, m.AvailableSlots })
            .ToListAsync(ct);

        var prevTotalBooked    = prevSlots.Sum(s => s.BookedSlots);
        var prevTotalAvailable = prevSlots.Sum(s => s.AvailableSlots);
        var prevUtilization    = prevTotalAvailable > 0
            ? Math.Round((decimal)prevTotalBooked / prevTotalAvailable * 100, 2)
            : 0m;

        // ── Compose response ──────────────────────────────────────────────────
        var cards = new[]
        {
            new KpiCardValue(
                KpiMetricType.NoShowRate,
                noShowRate,
                prevNoShowRate,
                ComputeChangePercent(noShowRate, prevNoShowRate)),

            new KpiCardValue(
                KpiMetricType.AppointmentUtilization,
                utilizationRate,
                prevUtilization,
                ComputeChangePercent(utilizationRate, prevUtilization)),

            new KpiCardValue(
                KpiMetricType.AverageWaitTime,
                avgWaitMinutes,
                prevAvgWait,
                ComputeChangePercent(avgWaitMinutes, prevAvgWait)),

            new KpiCardValue(
                KpiMetricType.BookingVolume,
                total,
                prevTotal,
                ComputeChangePercent(total, prevTotal)),
        };

        var summary = new KpiSummaryResponse(cards, DateTime.UtcNow, IsStale: false);
        _cache.Set(range, summary);
        return summary;
    }

    /// <inheritdoc/>
    public async Task<KpiTimeSeriesResponse> GetTimeSeriesAsync(
        KpiMetricType metric,
        DateRange range,
        CancellationToken ct = default)
    {
        var dailyMetrics = await _db.KpiDailyMetrics
            .Where(m => m.Date >= range.From && m.Date <= range.To)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        var points = dailyMetrics.Select(m => new KpiTimeSeriesPoint(
            m.Date,
            metric switch
            {
                KpiMetricType.NoShowRate             => m.NoShowRate,
                KpiMetricType.AppointmentUtilization => m.UtilizationRate,
                KpiMetricType.AverageWaitTime        => m.AverageWaitMinutes,
                KpiMetricType.BookingVolume          => m.BookingCount,
                _                                    => 0m,
            })).ToList();

        return new KpiTimeSeriesResponse(metric, points, DateTime.UtcNow, IsStale: false);
    }

    /// <inheritdoc/>
    public async Task<KpiExportResult> ExportAsync(KpiExportRequest request, CancellationToken ct = default)
    {
        var summary = await GetSummaryAsync(request.Range, ct);
        return request.Format switch
        {
            KpiExportFormat.Pdf => _renderer.RenderPdf(summary, request.Range),
            KpiExportFormat.Png => _renderer.RenderPng(summary, request.Range),
            _                   => throw new ArgumentOutOfRangeException(
                nameof(request), request.Format, "Unsupported export format."),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the percentage change between <paramref name="current"/> and
    /// <paramref name="previous"/>, or <c>null</c> when <paramref name="previous"/> is zero.
    /// </summary>
    private static decimal? ComputeChangePercent(decimal current, decimal previous)
    {
        if (previous == 0m) return null;
        return Math.Round((current - previous) / previous * 100, 1);
    }
}

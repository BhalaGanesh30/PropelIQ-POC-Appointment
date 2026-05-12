namespace PropelIQ.Modules.SharedServices.Application.Kpi;

/// <summary>
/// Service contract for KPI metric aggregation, time-series retrieval, and export (US_060).
///
/// <para>
/// Implemented by <c>KpiMetricsService</c> in the Infrastructure layer.
/// Consumed by <c>KpiDashboardController</c> (REST) and <c>KpiDistributionWorker</c> (scheduled email).
/// </para>
/// </summary>
public interface IKpiMetricsService
{
    /// <summary>
    /// Returns a summary of all four KPI card values for the given date range (AC-1, AC-2).
    ///
    /// <para>
    /// Results are served from the in-memory snapshot cache when available.
    /// <see cref="KpiSummaryResponse.IsStale"/> is <c>true</c> when the cached result is
    /// older than 1 hour (edge case 1).
    /// </para>
    ///
    /// <para>
    /// When no appointments exist for <paramref name="range"/>, all card values return 0 (edge case 2).
    /// </para>
    /// </summary>
    Task<KpiSummaryResponse> GetSummaryAsync(DateRange range, CancellationToken ct = default);

    /// <summary>
    /// Returns a daily time series for a single metric over the given date range (AC-2).
    ///
    /// <para>
    /// Reads from <c>kpi_daily_metrics</c>. Days with no data produce a 0-value point.
    /// </para>
    /// </summary>
    Task<KpiTimeSeriesResponse> GetTimeSeriesAsync(
        KpiMetricType   metric,
        DateRange       range,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a PDF or PNG export of the KPI summary for the given date range (AC-3).
    ///
    /// <para>AC-3 SLA: export must complete within 3 seconds for ranges ≤ 365 days.</para>
    /// </summary>
    Task<KpiExportResult> ExportAsync(KpiExportRequest request, CancellationToken ct = default);
}

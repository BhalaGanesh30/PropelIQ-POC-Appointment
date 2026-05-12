namespace PropelIQ.Modules.SharedServices.Application.Kpi;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>
/// The four operational KPI metrics tracked on the admin dashboard (US_060, AC-1).
/// </summary>
public enum KpiMetricType
{
    /// <summary>Percentage of booked appointments where the patient did not attend.</summary>
    NoShowRate,

    /// <summary>Percentage of available slots that were booked.</summary>
    AppointmentUtilization,

    /// <summary>Average time in minutes from scheduled appointment start to patient arrival.</summary>
    AverageWaitTime,

    /// <summary>Total confirmed appointment bookings within the selected period.</summary>
    BookingVolume,
}

/// <summary>Export format requested by the admin (US_060, AC-3).</summary>
public enum KpiExportFormat
{
    /// <summary>PDF export via QuestPDF.</summary>
    Pdf,

    /// <summary>PNG image export via QuestPDF image generation.</summary>
    Png,
}

// ── Value objects ─────────────────────────────────────────────────────────────

/// <summary>Inclusive date range used to scope all KPI queries (US_060, AC-2).</summary>
public sealed record DateRange(DateOnly From, DateOnly To);

/// <summary>
/// Aggregated KPI card value for a single metric over the selected period.
/// Includes a comparison against the immediately preceding period of equal length.
/// </summary>
public sealed record KpiCardValue(
    KpiMetricType   Metric,
    decimal         Value,
    decimal?        PreviousPeriodValue,
    decimal?        ChangePercent);

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>
/// Summary response containing one <see cref="KpiCardValue"/> for each metric type (US_060, AC-1).
///
/// <para>
/// <see cref="IsStale"/> is <c>true</c> when <see cref="ComputedAtUtc"/> is more than 1 hour
/// in the past (edge case 1 — stale-data warning shown in the UI).
/// </para>
/// </summary>
public sealed record KpiSummaryResponse(
    IReadOnlyList<KpiCardValue> Cards,
    DateTime                    ComputedAtUtc,
    bool                        IsStale);

/// <summary>Single daily data point within a KPI time-series (US_060, AC-2).</summary>
public sealed record KpiTimeSeriesPoint(DateOnly Date, decimal Value);

/// <summary>
/// Time-series response for chart rendering (US_060, AC-2).
///
/// <see cref="Points"/> is empty when no data exists for the selected period (edge case 2).
/// </summary>
public sealed record KpiTimeSeriesResponse(
    KpiMetricType                   Metric,
    IReadOnlyList<KpiTimeSeriesPoint> Points,
    DateTime                        ComputedAtUtc,
    bool                            IsStale);

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Request body for the export endpoint (US_060, AC-3).</summary>
public sealed record KpiExportRequest(DateRange Range, KpiExportFormat Format);

/// <summary>
/// Result of an export operation — binary content with MIME type and suggested file name.
/// </summary>
public sealed record KpiExportResult(byte[] Content, string ContentType, string FileName);

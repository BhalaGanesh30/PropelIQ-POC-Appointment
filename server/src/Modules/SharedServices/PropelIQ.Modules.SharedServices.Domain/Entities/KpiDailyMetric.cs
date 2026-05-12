namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Pre-computed daily KPI metrics snapshot (US_060, AC-1, AC-2).
///
/// Rows are written once per calendar day by a nightly aggregation pass or on-demand.
/// The table is append-only — rows are never updated after insertion.
/// Maps to <c>app.kpi_daily_metrics</c> (created by US_060 task_002 migration).
/// </summary>
public sealed class KpiDailyMetric
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Calendar date this metric row represents.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Percentage of booked appointments where the patient did not attend (0–100).</summary>
    public required decimal NoShowRate { get; init; }

    /// <summary>Percentage of available slots that were booked (0–100).</summary>
    public required decimal UtilizationRate { get; init; }

    /// <summary>Average time in minutes from scheduled appointment start to patient arrival.</summary>
    public required decimal AverageWaitMinutes { get; init; }

    /// <summary>Total confirmed bookings on this date.</summary>
    public required int BookingCount { get; init; }

    /// <summary>Total available appointment slots on this date.</summary>
    public required int AvailableSlots { get; init; }

    /// <summary>Total booked appointment slots on this date.</summary>
    public required int BookedSlots { get; init; }

    /// <summary>UTC timestamp when this row was computed or last refreshed.</summary>
    public DateTime ComputedAtUtc { get; init; } = DateTime.UtcNow;
}

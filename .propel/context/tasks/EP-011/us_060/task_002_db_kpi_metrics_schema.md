# Task - TASK_002

## Requirement Reference

- User Story: us_060
- Story Location: .propel/context/tasks/EP-011/us_060/us_060.md
- Acceptance Criteria:
  - AC-1: Given I open the KPI dashboard, When the page loads, Then charts for no-show rate, appointment utilization, average wait time, and booking volume are rendered within 3 seconds using the latest available data.
  - AC-4: Given a scheduled distribution is configured, When the schedule triggers (e.g., every Monday 8 AM), Then the KPI report is generated and emailed as a PDF to the configured recipient list.
- Edge Cases:
  - What happens if KPI data computation is delayed due to a large dataset? Charts show a loading state with a "Last updated" timestamp; stale data is shown with a staleness warning if more than 1 hour has elapsed.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | N/A | N/A |
| Database | PostgreSQL | 15.x |
| Library | EF Core with Npgsql | 8.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Create the PostgreSQL schema supporting KPI dashboard metrics. The `kpi_daily_metrics` table stores pre-computed daily aggregates (no-show rate, appointment utilization, average wait time, booking volume) derived from the `appointments` table, enabling sub-second query performance for dashboard date range filters (AC-1, AC-2). A `kpi_snapshot_refresh` function or scheduled EF Core migration seed computes daily values from appointments. The `kpi_distribution_log` table tracks scheduled KPI report email deliveries (AC-4) with status, recipient list, and retry tracking. Indexes support date-range lookups for chart rendering and staleness detection (edge case 1). The schema follows the insert-only daily pattern — one row per date — with a unique index on `date` to prevent duplicates.

## Dependent Tasks

- US_015 task_001 (requires base database schema and EF Core context)

## Impacted Components

- New: `server/src/PropelIQ.Infrastructure/Persistence/Entities/KpiDailyMetric.cs` (EF entity)
- New: `server/src/PropelIQ.Infrastructure/Persistence/Entities/KpiDistributionLog.cs` (EF entity)
- Modify: `server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs` (add DbSets)
- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/YYYYMMDD_AddKpiDashboardSchema.cs` (migration)

## Implementation Plan

1. **Create `KpiDailyMetric` entity** for pre-computed daily aggregates:

```csharp
// PropelIQ.Infrastructure/Persistence/Entities/
//   KpiDailyMetric.cs

public sealed class KpiDailyMetric
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public int BookingCount { get; set; }
    public int NoShowCount { get; set; }
    public decimal NoShowRate { get; set; }
    public int BookedSlots { get; set; }
    public int AvailableSlots { get; set; }
    public decimal UtilizationRate { get; set; }
    public decimal AverageWaitMinutes { get; set; }
    public DateTime ComputedAtUtc { get; set; }
}
```

Corresponding SQL schema:

```sql
CREATE TABLE kpi_daily_metrics (
    id              uuid PRIMARY KEY
                    DEFAULT gen_random_uuid(),
    date            date NOT NULL,
    booking_count   int NOT NULL DEFAULT 0,
    no_show_count   int NOT NULL DEFAULT 0,
    no_show_rate    numeric(5,2) NOT NULL DEFAULT 0,
    booked_slots    int NOT NULL DEFAULT 0,
    available_slots int NOT NULL DEFAULT 0,
    utilization_rate numeric(5,2) NOT NULL DEFAULT 0,
    average_wait_minutes
                    numeric(7,2) NOT NULL DEFAULT 0,
    computed_at_utc timestamptz NOT NULL
                    DEFAULT now()
);

-- Unique constraint: one row per date
CREATE UNIQUE INDEX
    uix_kpi_daily_metrics_date
    ON kpi_daily_metrics (date);

-- Date range query optimization
CREATE INDEX
    ix_kpi_daily_metrics_date_range
    ON kpi_daily_metrics (date DESC);
```

2. **Create `KpiDistributionLog` entity** for scheduled email tracking:

```csharp
// PropelIQ.Infrastructure/Persistence/Entities/
//   KpiDistributionLog.cs

public sealed class KpiDistributionLog
{
    public Guid Id { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DistributionStatus Status { get; set; }
    public string[] Recipients { get; set; }
        = Array.Empty<string>();
    public DateOnly ReportFromDate { get; set; }
    public DateOnly ReportToDate { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorDetails { get; set; }
}

public enum DistributionStatus
{
    Pending,
    Sent,
    Failed
}
```

Corresponding SQL schema:

```sql
CREATE TABLE kpi_distribution_log (
    id              uuid PRIMARY KEY
                    DEFAULT gen_random_uuid(),
    scheduled_at_utc timestamptz NOT NULL,
    sent_at_utc     timestamptz,
    status          varchar(20) NOT NULL
                    DEFAULT 'Pending',
    recipients      text[] NOT NULL
                    DEFAULT '{}',
    report_from_date date NOT NULL,
    report_to_date   date NOT NULL,
    attempt_count   int NOT NULL DEFAULT 0,
    error_details   text
);

-- Pending distribution polling index
CREATE INDEX
    ix_kpi_distribution_log_pending
    ON kpi_distribution_log (scheduled_at_utc)
    WHERE status = 'Pending';

-- History lookup
CREATE INDEX
    ix_kpi_distribution_log_status_date
    ON kpi_distribution_log (
        status, scheduled_at_utc DESC);
```

3. **Register DbSets** in `AppDbContext`:

```csharp
// In AppDbContext.cs — add:
public DbSet<KpiDailyMetric>
    KpiDailyMetrics => Set<KpiDailyMetric>();
public DbSet<KpiDistributionLog>
    KpiDistributionLogs =>
        Set<KpiDistributionLog>();
```

4. **Configure entity mappings** with EF Core Fluent API:

```csharp
// In OnModelCreating or separate
// IEntityTypeConfiguration classes:

builder.Entity<KpiDailyMetric>(entity =>
{
    entity.ToTable("kpi_daily_metrics");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.Date).IsUnique();
    entity.Property(e => e.NoShowRate)
        .HasPrecision(5, 2);
    entity.Property(e => e.UtilizationRate)
        .HasPrecision(5, 2);
    entity.Property(e => e.AverageWaitMinutes)
        .HasPrecision(7, 2);
});

builder.Entity<KpiDistributionLog>(entity =>
{
    entity.ToTable("kpi_distribution_log");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Status)
        .HasMaxLength(20)
        .HasDefaultValue("Pending");
    entity.HasIndex(e => e.ScheduledAtUtc)
        .HasFilter("status = 'Pending'");
});
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        └── PropelIQ.Infrastructure/
            └── Persistence/
                ├── AppDbContext.cs                   (modify)
                ├── Entities/
                │   ├── KpiDailyMetric.cs            (new)
                │   └── KpiDistributionLog.cs        (new)
                └── Migrations/
                    └── YYYYMMDD_AddKpiDashboardSchema.cs (new)
```

> Placeholder: Update on execution based on US_015 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Entities/KpiDailyMetric.cs | Entity for pre-computed daily KPI aggregates with date, counts, rates |
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Entities/KpiDistributionLog.cs | Entity for scheduled distribution tracking with status, recipients, retry |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add KpiDailyMetrics and KpiDistributionLogs DbSet properties and Fluent API config |

## External References

- EF Core Migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations
- Npgsql Array Mapping: https://www.npgsql.org/efcore/mapping/array.html
- PostgreSQL Partial Indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- PostgreSQL Date Types: https://www.postgresql.org/docs/15/datatype-datetime.html

## Build Commands

```bash
# Generate migration
cd server
dotnet ef migrations add AddKpiDashboardSchema \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api

# Verify tables exist
psql -d propeliq -c "\d kpi_daily_metrics"
psql -d propeliq -c "\d kpi_distribution_log"
```

## Implementation Validation Strategy

- [ ] `kpi_daily_metrics` table created with unique index on `date`
- [ ] `kpi_distribution_log` table created with partial index on pending status
- [ ] Numeric precision columns (no_show_rate, utilization_rate, average_wait_minutes) store correct decimal places
- [ ] Distribution log `recipients` column maps to PostgreSQL `text[]` array
- [ ] Migration applies cleanly and rolls back without errors
- [ ] DbSets registered in AppDbContext and queryable via LINQ

## Implementation Checklist

- [x] Create KpiDailyMetric entity with date, counts, rates, and computed_at_utc timestamp
- [x] Create KpiDistributionLog entity with status enum, recipients array, and retry tracking
- [x] Register DbSets in AppDbContext for both entities
- [x] Configure Fluent API mappings with table names, indexes, precision, and partial index filter
- [x] Generate and apply EF Core migration
- [x] Verify unique constraint on kpi_daily_metrics.date prevents duplicate rows

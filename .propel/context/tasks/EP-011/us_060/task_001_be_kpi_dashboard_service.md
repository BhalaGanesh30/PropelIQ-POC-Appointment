# Task - TASK_001

## Requirement Reference

- User Story: us_060
- Story Location: .propel/context/tasks/EP-011/us_060/us_060.md
- Acceptance Criteria:
  - AC-1: Given I open the KPI dashboard, When the page loads, Then charts for no-show rate, appointment utilization, average wait time, and booking volume are rendered within 3 seconds using the latest available data.
  - AC-2: Given the KPI charts are displayed, When I select a different date range, Then the charts update within 1 second to reflect the selected period.
  - AC-3: Given I want to share a chart, When I click "Export" on a chart, Then the chart is exported as a PNG or PDF within 3 seconds.
  - AC-4: Given a scheduled distribution is configured, When the schedule triggers (e.g., every Monday 8 AM), Then the KPI report is generated and emailed as a PDF to the configured recipient list.
- Edge Cases:
  - What happens if KPI data computation is delayed due to a large dataset? Charts show a loading state with a "Last updated" timestamp; stale data is shown with a staleness warning if more than 1 hour has elapsed.
  - How does the system handle an empty date range (no appointments in the selected period)? Charts render with zero values and a "No data for the selected period" annotation.

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
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL | 15.x |
| Library | QuestPDF | latest stable |
| Library | Polly | latest stable |
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

Implement the backend KPI dashboard service that aggregates operational metrics (no-show rate, appointment utilization, average wait time, booking volume) from the `appointments` table, exposes REST endpoints for the admin dashboard, provides chart data export as PNG/PDF via QuestPDF, and runs a scheduled distribution worker that emails KPI report PDFs to configured recipients. The service uses a pre-computed snapshot cache with staleness detection (edge case 1) and returns empty-period annotations when no data exists (edge case 2). Scheduled distribution reads recipient configuration from US_059's `IConfigurationService` (CommunicationTemplates category). All endpoints require Admin role authorization.

## Dependent Tasks

- US_060 task_002 (requires `kpi_daily_metrics` and `kpi_distribution_log` tables)
- US_059 task_001 (requires `IConfigurationService` for distribution settings)
- US_015 task_001 (requires Admin authorization infrastructure)

## Impacted Components

- New: `server/src/PropelIQ.Application/Services/KpiMetricsService.cs` (metric aggregation)
- New: `server/src/PropelIQ.Application/Interfaces/IKpiMetricsService.cs` (service contract)
- New: `server/src/PropelIQ.Application/Models/Kpi/KpiModels.cs` (DTOs)
- New: `server/src/PropelIQ.Application/Validators/KpiDateRangeValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Infrastructure/Services/KpiSnapshotCacheService.cs` (cached snapshots)
- New: `server/src/PropelIQ.Infrastructure/Services/KpiReportPdfRenderer.cs` (QuestPDF)
- New: `server/src/PropelIQ.Infrastructure/Workers/KpiDistributionWorker.cs` (scheduled email)
- New: `server/src/PropelIQ.Api/Controllers/KpiDashboardController.cs` (REST endpoints)

## Implementation Plan

1. **Define the service contract and DTOs**:

```csharp
// PropelIQ.Application/Interfaces/
//   IKpiMetricsService.cs
public interface IKpiMetricsService
{
    Task<KpiSummaryResponse> GetSummaryAsync(
        DateRange range,
        CancellationToken ct = default);

    Task<KpiTimeSeriesResponse>
        GetTimeSeriesAsync(
            KpiMetricType metric,
            DateRange range,
            CancellationToken ct = default);

    Task<KpiExportResult> ExportAsync(
        KpiExportRequest request,
        CancellationToken ct = default);
}

// PropelIQ.Application/Models/Kpi/KpiModels.cs
public enum KpiMetricType
{
    NoShowRate,
    AppointmentUtilization,
    AverageWaitTime,
    BookingVolume
}

public sealed record DateRange(
    DateOnly From, DateOnly To);

public sealed record KpiCardValue(
    KpiMetricType Metric,
    decimal Value,
    decimal? PreviousPeriodValue,
    decimal? ChangePercent);

public sealed record KpiSummaryResponse(
    IReadOnlyList<KpiCardValue> Cards,
    DateTime ComputedAtUtc,
    bool IsStale);

public sealed record KpiTimeSeriesPoint(
    DateOnly Date, decimal Value);

public sealed record KpiTimeSeriesResponse(
    KpiMetricType Metric,
    IReadOnlyList<KpiTimeSeriesPoint> Points,
    DateTime ComputedAtUtc,
    bool IsStale);

public sealed record KpiExportRequest(
    DateRange Range,
    KpiExportFormat Format);

public enum KpiExportFormat { Png, Pdf }

public sealed record KpiExportResult(
    byte[] Content,
    string ContentType,
    string FileName);
```

2. **Implement `KpiMetricsService`** with aggregation queries:

```csharp
// PropelIQ.Application/Services/
//   KpiMetricsService.cs
public sealed class KpiMetricsService
    : IKpiMetricsService
{
    private readonly AppDbContext _db;
    private readonly KpiSnapshotCacheService _cache;
    private readonly ILogger<KpiMetricsService> _log;

    // Constructor injection omitted for brevity

    public async Task<KpiSummaryResponse>
        GetSummaryAsync(
            DateRange range,
            CancellationToken ct)
    {
        var cached = _cache.TryGet(range);
        if (cached is not null) return cached;

        var appointments = _db.Appointments
            .Where(a =>
                DateOnly.FromDateTime(a.DateTime)
                    >= range.From
                && DateOnly.FromDateTime(a.DateTime)
                    <= range.To);

        var total = await appointments
            .CountAsync(ct);

        // No-show rate
        var noShows = await appointments
            .CountAsync(a =>
                a.Status == AppointmentStatus.NoShow,
                ct);
        var noShowRate = total > 0
            ? (decimal)noShows / total * 100 : 0;

        // Average wait time (minutes)
        var avgWait = await appointments
            .Where(a => a.ArrivedAt != null)
            .Select(a =>
                EF.Functions.DateDiffMinute(
                    a.DateTime, a.ArrivedAt!.Value))
            .DefaultIfEmpty(0)
            .AverageAsync(ct);

        // Booking volume
        var bookingVolume = total;

        // Appointment utilization
        // (booked / available from daily metrics)
        var utilization = await _db
            .KpiDailyMetrics
            .Where(m =>
                m.Date >= range.From
                && m.Date <= range.To)
            .Select(m => new {
                m.BookedSlots,
                m.AvailableSlots })
            .ToListAsync(ct);
        var totalBooked = utilization
            .Sum(u => u.BookedSlots);
        var totalAvailable = utilization
            .Sum(u => u.AvailableSlots);
        var utilizationRate = totalAvailable > 0
            ? (decimal)totalBooked
              / totalAvailable * 100 : 0;

        // Previous period comparison
        var periodLength =
            range.To.DayNumber
            - range.From.DayNumber + 1;
        var prevRange = new DateRange(
            range.From.AddDays(-periodLength),
            range.From.AddDays(-1));
        // Repeat queries for prevRange ...

        var now = DateTime.UtcNow;
        var summary = new KpiSummaryResponse(
            Cards: new[]
            {
                new KpiCardValue(
                    KpiMetricType.NoShowRate,
                    noShowRate, null, null),
                new KpiCardValue(
                    KpiMetricType
                        .AppointmentUtilization,
                    utilizationRate, null, null),
                new KpiCardValue(
                    KpiMetricType.AverageWaitTime,
                    (decimal)avgWait, null, null),
                new KpiCardValue(
                    KpiMetricType.BookingVolume,
                    bookingVolume, null, null)
            },
            ComputedAtUtc: now,
            IsStale: false);

        _cache.Set(range, summary);
        return summary;
    }

    public async Task<KpiTimeSeriesResponse>
        GetTimeSeriesAsync(
            KpiMetricType metric,
            DateRange range,
            CancellationToken ct)
    {
        // Query kpi_daily_metrics grouped by date
        // for the requested metric type.
        // Return daily points for chart rendering.
        var dailyMetrics = await _db
            .KpiDailyMetrics
            .Where(m =>
                m.Date >= range.From
                && m.Date <= range.To)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        var points = dailyMetrics.Select(m =>
            new KpiTimeSeriesPoint(
                m.Date,
                metric switch
                {
                    KpiMetricType.NoShowRate =>
                        m.NoShowRate,
                    KpiMetricType
                        .AppointmentUtilization =>
                        m.UtilizationRate,
                    KpiMetricType.AverageWaitTime =>
                        m.AverageWaitMinutes,
                    KpiMetricType.BookingVolume =>
                        m.BookingCount,
                    _ => 0
                }))
            .ToList();

        return new KpiTimeSeriesResponse(
            metric, points, DateTime.UtcNow,
            IsStale: false);
    }

    public async Task<KpiExportResult>
        ExportAsync(
            KpiExportRequest request,
            CancellationToken ct)
    {
        var summary = await GetSummaryAsync(
            request.Range, ct);

        return request.Format switch
        {
            KpiExportFormat.Pdf =>
                KpiReportPdfRenderer
                    .RenderPdf(summary,
                        request.Range),
            KpiExportFormat.Png =>
                KpiReportPdfRenderer
                    .RenderPng(summary,
                        request.Range),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
```

3. **Implement `KpiSnapshotCacheService`** for staleness detection (edge case 1):

```csharp
// PropelIQ.Infrastructure/Services/
//   KpiSnapshotCacheService.cs
public sealed class KpiSnapshotCacheService
{
    private readonly ConcurrentDictionary<
        string, (KpiSummaryResponse Data,
                 DateTime CachedAt)> _cache = new();

    private static readonly TimeSpan
        StalenessThreshold = TimeSpan.FromHours(1);

    public KpiSummaryResponse? TryGet(
        DateRange range)
    {
        var key = $"{range.From}_{range.To}";
        if (!_cache.TryGetValue(key, out var entry))
            return null;

        var isStale = DateTime.UtcNow - entry.CachedAt
            > StalenessThreshold;

        return entry.Data with { IsStale = isStale };
    }

    public void Set(
        DateRange range,
        KpiSummaryResponse data)
    {
        var key = $"{range.From}_{range.To}";
        _cache[key] =
            (data, DateTime.UtcNow);
    }

    public void Invalidate() => _cache.Clear();
}
```

4. **Implement `KpiReportPdfRenderer`** using QuestPDF (AC-3, AC-4):

```csharp
// PropelIQ.Infrastructure/Services/
//   KpiReportPdfRenderer.cs
public static class KpiReportPdfRenderer
{
    public static KpiExportResult RenderPdf(
        KpiSummaryResponse summary,
        DateRange range)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);

                page.Header().Text(
                    $"KPI Report — "
                    + $"{range.From:yyyy-MM-dd} to "
                    + $"{range.To:yyyy-MM-dd}")
                    .FontSize(18).Bold();

                page.Content().Column(col =>
                {
                    foreach (var card in
                        summary.Cards)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem()
                                .Text(card.Metric
                                    .ToString())
                                .FontSize(14);
                            row.RelativeItem()
                                .Text($"{card.Value:F1}")
                                .FontSize(14).Bold();
                        });
                    }

                    if (summary.IsStale)
                    {
                        col.Item().Text(
                            "⚠ Data may be stale — "
                            + "last computed "
                            + summary.ComputedAtUtc
                                .ToString("g"))
                            .FontColor(Colors
                                .Orange.Medium);
                    }
                });

                page.Footer().AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Generated ");
                        x.Span(DateTime.UtcNow
                            .ToString("g"));
                    });
            });
        });

        var bytes = document.GeneratePdf();
        return new KpiExportResult(
            bytes,
            "application/pdf",
            $"kpi-report-{range.From:yyyyMMdd}"
            + $"-{range.To:yyyyMMdd}.pdf");
    }

    public static KpiExportResult RenderPng(
        KpiSummaryResponse summary,
        DateRange range)
    {
        // Generate PDF first then convert
        // first page to PNG via QuestPDF
        // ImageGenerationSettings
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.Content().Column(col =>
                {
                    foreach (var card in
                        summary.Cards)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem()
                                .Text(card.Metric
                                    .ToString());
                            row.RelativeItem()
                                .Text(
                                    $"{card.Value:F1}")
                                .Bold();
                        });
                    }
                });
            });
        });

        var images = document.GenerateImages();
        var bytes = images.First();
        return new KpiExportResult(
            bytes,
            "image/png",
            $"kpi-chart-{range.From:yyyyMMdd}"
            + $"-{range.To:yyyyMMdd}.png");
    }
}
```

5. **Implement `KpiDistributionWorker`** for scheduled email distribution (AC-4):

```csharp
// PropelIQ.Infrastructure/Workers/
//   KpiDistributionWorker.cs
public sealed class KpiDistributionWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<KpiDistributionWorker>
        _log;

    // Constructor injection omitted for brevity

    protected override async Task ExecuteAsync(
        CancellationToken ct)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(ct))
        {
            await using var scope =
                _scopes.CreateAsyncScope();
            var config = scope.ServiceProvider
                .GetRequiredService<
                    IConfigurationService>();
            var metrics = scope.ServiceProvider
                .GetRequiredService<
                    IKpiMetricsService>();
            var emailSender = scope.ServiceProvider
                .GetRequiredService<IEmailSender>();
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            // Read distribution config from
            // CommunicationTemplates category
            var configSnapshot = await config
                .GetCurrentAsync(
                    ConfigurationCategory
                        .CommunicationTemplates, ct);

            var schedule = configSnapshot.Values
                .GetValueOrDefault(
                    "kpiDistributionSchedule");
            if (schedule is null) continue;

            // Check if distribution is due
            var pendingDistro = await db
                .KpiDistributionLogs
                .Where(l =>
                    l.Status ==
                        DistributionStatus.Pending
                    && l.ScheduledAtUtc
                        <= DateTime.UtcNow)
                .FirstOrDefaultAsync(ct);

            if (pendingDistro is null) continue;

            var range = new DateRange(
                DateOnly.FromDateTime(
                    DateTime.UtcNow.AddDays(-7)),
                DateOnly.FromDateTime(
                    DateTime.UtcNow.AddDays(-1)));

            var export = await metrics.ExportAsync(
                new KpiExportRequest(range,
                    KpiExportFormat.Pdf), ct);

            // Email with Polly retry
            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType =
                        DelayBackoffType.Exponential
                })
                .Build();

            await pipeline.ExecuteAsync(
                async token =>
                {
                    await emailSender.SendAsync(
                        pendingDistro.Recipients,
                        "Weekly KPI Report",
                        "Attached is the weekly "
                        + "KPI report.",
                        export.Content,
                        export.FileName,
                        export.ContentType,
                        token);
                }, ct);

            pendingDistro.Status =
                DistributionStatus.Sent;
            pendingDistro.SentAtUtc =
                DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
```

6. **Implement `KpiDashboardController`** with Admin-only endpoints:

```csharp
// PropelIQ.Api/Controllers/
//   KpiDashboardController.cs
[ApiController]
[Route("api/v1/admin/kpi")]
[Authorize(Roles = "Admin")]
public sealed class KpiDashboardController
    : ControllerBase
{
    private readonly IKpiMetricsService _metrics;
    private readonly IValidator<DateRange>
        _rangeValidator;

    // Constructor injection omitted for brevity

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var range = new DateRange(from, to);
        var validation = await _rangeValidator
            .ValidateAsync(range, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors
                .Select(e => e.ErrorMessage));

        var result = await _metrics
            .GetSummaryAsync(range, ct);
        return Ok(result);
    }

    [HttpGet("timeseries/{metric}")]
    public async Task<IActionResult> GetTimeSeries(
        KpiMetricType metric,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var range = new DateRange(from, to);
        var validation = await _rangeValidator
            .ValidateAsync(range, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors
                .Select(e => e.ErrorMessage));

        var result = await _metrics
            .GetTimeSeriesAsync(metric, range, ct);
        return Ok(result);
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromBody] KpiExportRequest request,
        CancellationToken ct)
    {
        var validation = await _rangeValidator
            .ValidateAsync(request.Range, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors
                .Select(e => e.ErrorMessage));

        var result = await _metrics
            .ExportAsync(request, ct);
        return File(result.Content,
            result.ContentType,
            result.FileName);
    }
}
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       └── KpiDashboardController.cs       (new)
        ├── PropelIQ.Application/
        │   ├── Interfaces/
        │   │   └── IKpiMetricsService.cs           (new)
        │   ├── Models/
        │   │   └── Kpi/
        │   │       └── KpiModels.cs                (new)
        │   ├── Validators/
        │   │   └── KpiDateRangeValidator.cs        (new)
        │   └── Services/
        │       └── KpiMetricsService.cs            (new)
        └── PropelIQ.Infrastructure/
            ├── Services/
            │   ├── KpiSnapshotCacheService.cs       (new)
            │   └── KpiReportPdfRenderer.cs          (new)
            └── Workers/
                └── KpiDistributionWorker.cs         (new)
```

> Placeholder: Update on execution based on US_060 task_002 and US_059 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Interfaces/IKpiMetricsService.cs | Service contract for KPI summary, time series, and export operations |
| CREATE | server/src/PropelIQ.Application/Models/Kpi/KpiModels.cs | DTOs for metric types, date ranges, card values, time series points, export requests |
| CREATE | server/src/PropelIQ.Application/Validators/KpiDateRangeValidator.cs | FluentValidation for date range (From <= To, max 365 days, not future) |
| CREATE | server/src/PropelIQ.Application/Services/KpiMetricsService.cs | Aggregation queries against appointments and kpi_daily_metrics with snapshot caching |
| CREATE | server/src/PropelIQ.Infrastructure/Services/KpiSnapshotCacheService.cs | ConcurrentDictionary cache with 1-hour staleness threshold |
| CREATE | server/src/PropelIQ.Infrastructure/Services/KpiReportPdfRenderer.cs | QuestPDF rendering for PDF and PNG export of KPI summaries |
| CREATE | server/src/PropelIQ.Infrastructure/Workers/KpiDistributionWorker.cs | BackgroundService with PeriodicTimer for scheduled PDF email distribution via Polly retry |
| CREATE | server/src/PropelIQ.Api/Controllers/KpiDashboardController.cs | Admin-only REST endpoints at /api/v1/admin/kpi/ |

## External References

- QuestPDF Documentation: https://www.questpdf.com/getting-started.html
- Polly Resilience Pipelines: https://www.pollydocs.org/strategies/retry.html
- ASP.NET Core BackgroundService: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- EF Core Aggregate Queries: https://learn.microsoft.com/en-us/ef/core/querying/
- FluentValidation for ASP.NET Core: https://docs.fluentvalidation.net/en/latest/aspnet.html

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run backend
dotnet run --project src/PropelIQ.Api

# Verify endpoints:
# GET  /api/v1/admin/kpi/summary?from=2026-01-01&to=2026-03-31
# GET  /api/v1/admin/kpi/timeseries/NoShowRate?from=2026-01-01&to=2026-03-31
# POST /api/v1/admin/kpi/export
#   Body: { "range": { "from": "2026-01-01", "to": "2026-03-31" }, "format": "Pdf" }
```

## Implementation Validation Strategy

- [ ] Summary endpoint returns 4 KPI cards with correct values for date range
- [ ] Time series endpoint returns daily data points for each metric type
- [ ] Staleness detection flags responses older than 1 hour (edge case 1)
- [ ] Empty date range returns zero values with IsStale = false (edge case 2)
- [ ] PDF export generates valid PDF with KPI data within 3 seconds (AC-3)
- [ ] PNG export generates valid image within 3 seconds (AC-3)
- [ ] Distribution worker sends email with PDF attachment on schedule (AC-4)
- [ ] Polly retry handles transient email failures

## Implementation Checklist

- [ ] Define IKpiMetricsService contract with GetSummaryAsync, GetTimeSeriesAsync, ExportAsync
- [ ] Create KPI DTOs (KpiMetricType enum, DateRange, KpiCardValue, KpiSummaryResponse, KpiTimeSeriesResponse, KpiExportRequest)
- [ ] Implement KpiMetricsService with aggregation queries against appointments and kpi_daily_metrics tables
- [ ] Implement KpiSnapshotCacheService with ConcurrentDictionary and 1-hour staleness threshold
- [ ] Implement KpiReportPdfRenderer with QuestPDF for PDF and PNG export
- [ ] Implement KpiDistributionWorker with PeriodicTimer, IConfigurationService integration, and Polly retry
- [ ] Implement KpiDashboardController with Admin-authorized endpoints (summary, timeseries, export)
- [ ] Add FluentValidation for DateRange (From <= To, max span 365 days, To not in future)

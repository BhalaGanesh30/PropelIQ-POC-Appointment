# Task - TASK_002

## Requirement Reference

- User Story: us_028
- Story Location: .propel/context/tasks/EP-003/us_028/us_028.md
- Acceptance Criteria:
  - AC-3: Given a High-risk appointment is detected, When 24 hours before the appointment time, Then the staff member is surfaced a risk indicator prompt to consider manual follow-up.
  - AC-4: Given the risk model is invoked, When the scoring request is processed, Then the response is returned within 2.5 seconds p95 and the result is cached against the appointment record.
- Edge Cases:
  - How does the system handle risk score staleness? Scores are recalculated when appointment details change, e.g., reschedule, or when 24 hours have elapsed since the last score.

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
| Database | PostgreSQL with pgvector | 15.x |
| Library | EF Core (Npgsql) | latest stable |
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

Implement the `GET /api/v1/appointments/risk-scores` API endpoint for the queue dashboard to retrieve risk scores for upcoming appointments, the `RiskScoreRefreshWorker` that triggers score recalculation for stale or missing scores, and the `HighRiskNotificationWorker` that surfaces staff follow-up prompts 24 hours before high-risk appointments (AC-3). The API endpoint returns a list of appointments with their cached risk scores for a given date range — if a score is stale (>24h since `RiskScoredAt`) or missing, it invokes `INoShowRiskScoringService.ScoreAsync` inline and returns the fresh result, still within the 2.5s p95 budget (AC-4) because the AI task_001 handles caching. The `RiskScoreRefreshWorker` is a `BackgroundService` with a `PeriodicTimer` (every 30 minutes) that pre-computes risk scores for appointments in the next 7 days that are stale or unscored — this reduces inline scoring latency for the dashboard. The `HighRiskNotificationWorker` runs every hour, queries appointments with `RiskLevel = High` that are 24 hours away (±30 minute window), and publishes a `HighRiskAlertEvent` to a `System.Threading.Channels` channel consumed by the notification infrastructure to alert assigned staff (AC-3). On reschedule (edge case 2), the `BookingRescheduledEventHandler` (from US_022 task_001) is extended to clear the cached risk score, forcing recalculation.

## Dependent Tasks

- US_028 task_001 (requires INoShowRiskScoringService, NoShowRiskResult, Appointment risk columns)
- US_022 task_001 (requires BookingRescheduledEventHandler for score invalidation on reschedule)

## Impacted Components

- New: `server/src/PropelIQ.Api/Controllers/RiskScoreController.cs` (GET endpoint for queue dashboard)
- New: `server/src/PropelIQ.Infrastructure/AI/RiskScoreRefreshWorker.cs` (BackgroundService pre-computing stale scores)
- New: `server/src/PropelIQ.Infrastructure/AI/HighRiskNotificationWorker.cs` (BackgroundService for 24h high-risk staff alerts)
- New: `server/src/PropelIQ.Application/AI/Models/HighRiskAlertEvent.cs` (event published to Channel)
- Modify: `server/src/PropelIQ.Infrastructure/Booking/BookingRescheduledEventHandler.cs` (clear cached risk score on reschedule)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register workers and Channel)

## Implementation Plan

1. **Create `RiskScoreController`** with GET endpoint:

```csharp
// server/src/PropelIQ.Api/Controllers/RiskScoreController.cs
namespace PropelIQ.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
[Authorize(Roles = "Staff")]
public sealed class RiskScoreController : ControllerBase
{
    private readonly INoShowRiskScoringService _scoringService;
    private readonly IAppointmentRepository _appointmentRepo;

    public RiskScoreController(
        INoShowRiskScoringService scoringService,
        IAppointmentRepository appointmentRepo)
    {
        _scoringService = scoringService;
        _appointmentRepo = appointmentRepo;
    }

    // AC-4: Cached scores, inline recalc if stale
    [HttpGet("risk-scores")]
    [ProducesResponseType(
        typeof(IReadOnlyList<AppointmentRiskDto>), 200)]
    public async Task<IActionResult> GetRiskScores(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var appointments = await _appointmentRepo
            .GetUpcomingByDateRangeAsync(from, to, ct);

        var results = new List<AppointmentRiskDto>(
            appointments.Count);

        foreach (var appt in appointments)
        {
            var score = await _scoringService
                .ScoreAsync(appt.AppointmentId, ct);

            results.Add(new AppointmentRiskDto(
                appt.AppointmentId,
                appt.PatientName,
                appt.AppointmentDate,
                appt.AppointmentType,
                appt.Status,
                score.RiskLevel,
                score.Confidence,
                score.Features.Select(f =>
                    new RiskFeatureDto(
                        f.Name, f.Contribution))
                    .ToList()));
        }

        return Ok(results);
    }
}

public sealed record AppointmentRiskDto(
    Guid AppointmentId,
    string PatientName,
    DateTimeOffset AppointmentDate,
    string AppointmentType,
    string Status,
    string RiskLevel,
    double Confidence,
    IReadOnlyList<RiskFeatureDto> Features);

public sealed record RiskFeatureDto(
    string Name,
    string Contribution);
```

2. **Create `RiskScoreRefreshWorker`** to pre-compute stale scores:

```csharp
// server/src/PropelIQ.Infrastructure/AI/
//   RiskScoreRefreshWorker.cs
namespace PropelIQ.Infrastructure.AI;

public sealed class RiskScoreRefreshWorker
    : BackgroundService
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMinutes(30);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RiskScoreRefreshWorker> _logger;

    public RiskScoreRefreshWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<RiskScoreRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await RefreshStaleScoresAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Risk score refresh tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(
            stoppingToken));
    }

    private async Task RefreshStaleScoresAsync(
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<IAppointmentRepository>();
        var scorer = scope.ServiceProvider
            .GetRequiredService<INoShowRiskScoringService>();

        var now = _timeProvider.GetUtcNow();
        var staleThreshold = now -
            NoShowRiskDefaults.StalenessThreshold;

        // Appointments in next 7 days with stale or no score
        var stale = await repo
            .GetAppointmentsNeedingRiskScoreAsync(
                now, now.AddDays(7),
                staleThreshold, BatchSize, ct);

        if (stale.Count == 0) return;

        _logger.LogInformation(
            "Refreshing {Count} stale risk scores",
            stale.Count);

        foreach (var apptId in stale)
        {
            try
            {
                await scorer.ScoreAsync(apptId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to refresh risk for {Id}",
                    apptId);
            }
        }
    }
}
```

3. **Create `HighRiskNotificationWorker`** for 24h staff alerts:

```csharp
// server/src/PropelIQ.Infrastructure/AI/
//   HighRiskNotificationWorker.cs
namespace PropelIQ.Infrastructure.AI;

public sealed class HighRiskNotificationWorker
    : BackgroundService
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromHours(1);
    private static readonly TimeSpan AlertWindow =
        TimeSpan.FromHours(24);
    private static readonly TimeSpan WindowTolerance =
        TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChannelWriter<HighRiskAlertEvent> _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HighRiskNotificationWorker> _logger;

    public HighRiskNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ChannelWriter<HighRiskAlertEvent> writer,
        TimeProvider timeProvider,
        ILogger<HighRiskNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _writer = writer;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await CheckHighRiskAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "High-risk notification tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(
            stoppingToken));
    }

    // AC-3: Alert staff 24h before high-risk appointments
    private async Task CheckHighRiskAsync(
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<IAppointmentRepository>();

        var now = _timeProvider.GetUtcNow();
        var windowStart = now + AlertWindow - WindowTolerance;
        var windowEnd = now + AlertWindow + WindowTolerance;

        var highRisk = await repo
            .GetHighRiskAppointmentsInWindowAsync(
                windowStart, windowEnd, ct);

        foreach (var appt in highRisk)
        {
            await _writer.WriteAsync(
                new HighRiskAlertEvent(
                    appt.AppointmentId,
                    appt.PatientName,
                    appt.AppointmentDate,
                    appt.RiskLevel!,
                    appt.RiskConfidence ?? 0.0),
                ct);

            _logger.LogInformation(
                "High-risk alert for appointment {Id} " +
                "at {Date}",
                appt.AppointmentId,
                appt.AppointmentDate);
        }
    }
}
```

```csharp
// server/src/PropelIQ.Application/AI/Models/
//   HighRiskAlertEvent.cs
namespace PropelIQ.Application.AI.Models;

public sealed record HighRiskAlertEvent(
    Guid AppointmentId,
    string PatientName,
    DateTimeOffset AppointmentDate,
    string RiskLevel,
    double Confidence);
```

4. **Extend `BookingRescheduledEventHandler`** to invalidate cached risk score:

```csharp
// In BookingRescheduledEventHandler.HandleAsync
// After existing reschedule logic (from US_022 task_001):
// Edge case 2: Invalidate cached risk score on reschedule
await appointmentRepo.ClearRiskScoreAsync(
    evt.AppointmentId, stoppingToken);
```

```csharp
// Add to IAppointmentRepository
Task ClearRiskScoreAsync(
    Guid appointmentId,
    CancellationToken ct = default);

Task<IReadOnlyList<Guid>>
    GetAppointmentsNeedingRiskScoreAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset staleThreshold,
        int limit,
        CancellationToken ct = default);

Task<IReadOnlyList<Appointment>>
    GetHighRiskAppointmentsInWindowAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);
```

```csharp
// EF Core implementations
public async Task ClearRiskScoreAsync(
    Guid appointmentId,
    CancellationToken ct = default)
{
    await _db.Appointments
        .Where(a => a.AppointmentId == appointmentId)
        .ExecuteUpdateAsync(s => s
            .SetProperty(a => a.RiskLevel, (string?)null)
            .SetProperty(a => a.RiskConfidence,
                (double?)null)
            .SetProperty(a => a.RiskFeatures, (string?)null)
            .SetProperty(a => a.RiskScoredAt,
                (DateTimeOffset?)null),
            ct);
}

public async Task<IReadOnlyList<Guid>>
    GetAppointmentsNeedingRiskScoreAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset staleThreshold,
        int limit,
        CancellationToken ct = default)
{
    return await _db.Appointments
        .Where(a =>
            a.AppointmentDate >= from
            && a.AppointmentDate <= to
            && a.Status == "Confirmed"
            && (a.RiskScoredAt == null
                || a.RiskScoredAt < staleThreshold))
        .OrderBy(a => a.AppointmentDate)
        .Take(limit)
        .Select(a => a.AppointmentId)
        .ToListAsync(ct);
}

public async Task<IReadOnlyList<Appointment>>
    GetHighRiskAppointmentsInWindowAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
{
    return await _db.Appointments
        .Where(a =>
            a.RiskLevel == "High"
            && a.AppointmentDate >= windowStart
            && a.AppointmentDate <= windowEnd
            && a.Status == "Confirmed")
        .ToListAsync(ct);
}
```

5. **Register workers and Channel in DI**:

```csharp
// In Program.cs
var highRiskChannel = Channel.CreateUnbounded<HighRiskAlertEvent>();
services.AddSingleton(highRiskChannel.Reader);
services.AddSingleton(highRiskChannel.Writer);
services.AddHostedService<RiskScoreRefreshWorker>();
services.AddHostedService<HighRiskNotificationWorker>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   ├── Controllers/
        │   │   └── RiskScoreController.cs              (new)
        │   └── Program.cs                              (modify — register workers, Channel)
        ├── PropelIQ.Application/
        │   ├── AI/
        │   │   ├── INoShowRiskScoringService.cs        (existing from task_001)
        │   │   └── Models/
        │   │       ├── NoShowRiskResult.cs              (existing from task_001)
        │   │       └── HighRiskAlertEvent.cs            (new)
        │   └── Booking/
        │       └── IAppointmentRepository.cs            (modify — add query methods)
        └── PropelIQ.Infrastructure/
            ├── AI/
            │   ├── RiskScoreRefreshWorker.cs            (new)
            │   └── HighRiskNotificationWorker.cs        (new)
            ├── Booking/
            │   ├── BookingRescheduledEventHandler.cs    (modify — clear risk score)
            │   └── AppointmentRepository.cs             (modify — add query methods)
            └── Data/
                └── AppDbContext.cs                       (no changes)
```

> Placeholder: Update on execution based on US_028 task_001 and US_022 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Api/Controllers/RiskScoreController.cs | GET /api/v1/appointments/risk-scores with inline stale recalc |
| CREATE | server/src/PropelIQ.Infrastructure/AI/RiskScoreRefreshWorker.cs | 30-min PeriodicTimer pre-computing stale scores for next 7 days |
| CREATE | server/src/PropelIQ.Infrastructure/AI/HighRiskNotificationWorker.cs | 1-hour PeriodicTimer publishing HighRiskAlertEvent for 24h window |
| CREATE | server/src/PropelIQ.Application/AI/Models/HighRiskAlertEvent.cs | Event record for staff high-risk notification |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/BookingRescheduledEventHandler.cs | Clear cached risk score on reschedule |
| MODIFY | server/src/PropelIQ.Application/Booking/IAppointmentRepository.cs | Add ClearRiskScoreAsync, GetAppointmentsNeedingRiskScoreAsync, GetHighRiskAppointmentsInWindowAsync |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/AppointmentRepository.cs | EF Core implementations for new repository methods |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register workers, HighRiskAlertEvent Channel |

## External References

- BackgroundService with PeriodicTimer: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- ASP.NET Core Authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run (workers start automatically)
dotnet run --project src/PropelIQ.Api

# Verify risk scores endpoint
# GET https://localhost:5001/api/v1/appointments/risk-scores?from=2026-04-17&to=2026-04-24
```

## Implementation Validation Strategy

- [ ] GET /api/v1/appointments/risk-scores returns risk data for date range (AC-4)
- [ ] Stale scores trigger inline recalculation within 2.5s p95 budget
- [ ] RiskScoreRefreshWorker pre-computes scores every 30 minutes for next 7 days
- [ ] HighRiskNotificationWorker publishes alert events 24h before high-risk appointments (AC-3)
- [ ] Reschedule clears cached risk score forcing recalculation (edge case 2)
- [ ] Endpoint requires Staff role authorization
- [ ] Workers handle errors gracefully without crashing

## Implementation Checklist

- [ ] Create RiskScoreController with GET risk-scores endpoint (Staff authorized)
- [ ] Create RiskScoreRefreshWorker with 30-minute PeriodicTimer and batch processing
- [ ] Create HighRiskNotificationWorker with 1-hour PeriodicTimer and 24h±30min window
- [ ] Create HighRiskAlertEvent record and register Channel in DI
- [ ] Add ClearRiskScoreAsync to IAppointmentRepository and implementation
- [ ] Add GetAppointmentsNeedingRiskScoreAsync for stale score queries
- [ ] Add GetHighRiskAppointmentsInWindowAsync for high-risk window queries
- [ ] Extend BookingRescheduledEventHandler to clear cached risk score on reschedule

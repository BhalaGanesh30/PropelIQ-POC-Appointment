# Task - TASK_002

## Requirement Reference

- User Story: us_026
- Story Location: .propel/context/tasks/EP-003/us_026/us_026.md
- Acceptance Criteria:
  - AC-2: Given a reminder event is due, When the scheduler evaluates pending reminders, Then reminders within a 1-minute tolerance of their scheduled time are dispatched.
- Edge Cases:
  - What happens if the reminder scheduler restarts mid-schedule? Scheduled reminders are persisted in the database; on restart, the scheduler resumes from the next pending event.
  - How does the system avoid duplicate reminder sends after a retry? Each ReminderEvent has a unique idempotency key; duplicate dispatch is prevented by checking sent status before delivery.

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

Implement the `ReminderDispatchWorker` — a `BackgroundService` using `PeriodicTimer` (1-minute interval) that polls the `ReminderEvent` table for due reminders and dispatches them through the configured notification channels (email, SMS). AC-2 requires reminders within a 1-minute tolerance of their `ScheduledAt` time to be dispatched on each tick. The worker queries for `ReminderEvent` rows where `SendStatus = Pending` and `ScheduledAt <= now + 1 minute` (the tolerance window). Before dispatching, the worker atomically transitions each reminder's `SendStatus` from `Pending` to `Sending` using optimistic concurrency to prevent duplicate sends across multiple worker instances (edge case 2). On successful dispatch via `INotificationService`, the status transitions to `Sent` with `SentAt` timestamp. On failure, the worker increments `RetryCount` and resets `SendStatus` to `Pending` for the next tick — up to a maximum of 3 retries (Decision 6: explicit retry with exponential backoff via Polly). After 3 retries the status transitions to `Failed`. The worker is resilient to restarts (edge case 1) because all state is persisted in the database — on startup the worker immediately picks up any overdue pending reminders. The dispatch pipeline uses Polly `RetryPolicy` with exponential backoff for transient failures from email/SMS providers.

## Dependent Tasks

- US_026 task_001 (requires ReminderSchedulingService, ReminderEventRepository, ReminderEvent entity with IdempotencyKey)
- US_009 (Foundational — requires ReminderEvent entity and table in database)

## Impacted Components

- New: `server/src/PropelIQ.Infrastructure/Reminders/ReminderDispatchWorker.cs` (BackgroundService with PeriodicTimer polling)
- New: `server/src/PropelIQ.Application/Reminders/IReminderDispatchRepository.cs` (query due reminders, transition status)
- New: `server/src/PropelIQ.Infrastructure/Reminders/ReminderDispatchRepository.cs` (EF Core implementation)
- New: `server/src/PropelIQ.Application/Reminders/INotificationDispatcher.cs` (abstraction over email/SMS send)
- New: `server/src/PropelIQ.Infrastructure/Reminders/NotificationDispatcher.cs` (delegates to email and SMS providers with Polly retry)
- Modify: `server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs` (add composite index on ScheduledAt + SendStatus for dispatch query)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register ReminderDispatchWorker as hosted service)

## Implementation Plan

1. **Create `IReminderDispatchRepository`** for querying due reminders and transitioning status:

```csharp
// server/src/PropelIQ.Application/Reminders/
//   IReminderDispatchRepository.cs
namespace PropelIQ.Application.Reminders;

public interface IReminderDispatchRepository
{
    /// <summary>
    /// AC-2: Query reminders where SendStatus = Pending
    /// and ScheduledAt <= now + tolerance (1 minute).
    /// </summary>
    Task<IReadOnlyList<ReminderEvent>> GetDueRemindersAsync(
        DateTimeOffset now,
        TimeSpan tolerance,
        int batchSize,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically transition Pending -> Sending.
    /// Returns false if already claimed by another worker.
    /// </summary>
    Task<bool> TryClaimForDispatchAsync(
        Guid reminderId,
        CancellationToken ct = default);

    /// <summary>
    /// Mark reminder as Sent with timestamp.
    /// </summary>
    Task MarkSentAsync(
        Guid reminderId,
        DateTimeOffset sentAt,
        CancellationToken ct = default);

    /// <summary>
    /// Increment retry count and reset to Pending,
    /// or mark Failed if max retries exceeded.
    /// </summary>
    Task MarkRetryOrFailedAsync(
        Guid reminderId,
        int maxRetries,
        CancellationToken ct = default);
}
```

2. **Implement `ReminderDispatchRepository`**:

```csharp
// server/src/PropelIQ.Infrastructure/Reminders/
//   ReminderDispatchRepository.cs
namespace PropelIQ.Infrastructure.Reminders;

public sealed class ReminderDispatchRepository
    : IReminderDispatchRepository
{
    private readonly AppDbContext _db;

    public ReminderDispatchRepository(AppDbContext db) => _db = db;

    // AC-2: 1-minute tolerance window
    public async Task<IReadOnlyList<ReminderEvent>>
        GetDueRemindersAsync(
            DateTimeOffset now,
            TimeSpan tolerance,
            int batchSize,
            CancellationToken ct = default)
    {
        var cutoff = now + tolerance;

        return await _db.ReminderEvents
            .Where(r =>
                r.SendStatus == SendStatus.Pending
                && r.ScheduledAt <= cutoff)
            .OrderBy(r => r.ScheduledAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    // Optimistic concurrency: only claim if still Pending
    public async Task<bool> TryClaimForDispatchAsync(
        Guid reminderId,
        CancellationToken ct = default)
    {
        var affected = await _db.ReminderEvents
            .Where(r =>
                r.ReminderId == reminderId
                && r.SendStatus == SendStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    r => r.SendStatus,
                    SendStatus.Sending),
                ct);

        return affected > 0;
    }

    public async Task MarkSentAsync(
        Guid reminderId,
        DateTimeOffset sentAt,
        CancellationToken ct = default)
    {
        await _db.ReminderEvents
            .Where(r => r.ReminderId == reminderId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.SendStatus, SendStatus.Sent)
                    .SetProperty(r => r.SentAt, sentAt),
                ct);
    }

    public async Task MarkRetryOrFailedAsync(
        Guid reminderId,
        int maxRetries,
        CancellationToken ct = default)
    {
        var reminder = await _db.ReminderEvents
            .FirstAsync(r => r.ReminderId == reminderId, ct);

        reminder.RetryCount++;

        reminder.SendStatus = reminder.RetryCount >= maxRetries
            ? SendStatus.Failed
            : SendStatus.Pending;

        await _db.SaveChangesAsync(ct);
    }
}
```

3. **Create `INotificationDispatcher` and Polly-wrapped implementation**:

```csharp
// server/src/PropelIQ.Application/Reminders/
//   INotificationDispatcher.cs
namespace PropelIQ.Application.Reminders;

public interface INotificationDispatcher
{
    Task DispatchAsync(
        ReminderEvent reminder,
        CancellationToken ct = default);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Reminders/
//   NotificationDispatcher.cs
namespace PropelIQ.Infrastructure.Reminders;

public sealed class NotificationDispatcher
    : INotificationDispatcher
{
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly ILogger<NotificationDispatcher> _logger;

    // Decision 6: Polly retry with exponential backoff
    private static readonly ResiliencePipeline RetryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();

    public NotificationDispatcher(
        IEmailService emailService,
        ISmsService smsService,
        ILogger<NotificationDispatcher> logger)
    {
        _emailService = emailService;
        _smsService = smsService;
        _logger = logger;
    }

    public async Task DispatchAsync(
        ReminderEvent reminder,
        CancellationToken ct = default)
    {
        await RetryPipeline.ExecuteAsync(async token =>
        {
            switch (reminder.Channel)
            {
                case "Email":
                    await _emailService.SendReminderAsync(
                        reminder, token);
                    break;
                case "SMS":
                    await _smsService.SendReminderAsync(
                        reminder, token);
                    break;
                default:
                    _logger.LogWarning(
                        "Unknown channel {Channel} for {Id}",
                        reminder.Channel, reminder.ReminderId);
                    break;
            }
        }, ct);
    }
}
```

4. **Implement `ReminderDispatchWorker`** BackgroundService:

```csharp
// server/src/PropelIQ.Infrastructure/Reminders/
//   ReminderDispatchWorker.cs
namespace PropelIQ.Infrastructure.Reminders;

public sealed class ReminderDispatchWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Tolerance =
        TimeSpan.FromMinutes(1);
    private const int BatchSize = 50;
    private const int MaxRetries = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderDispatchWorker> _logger;

    public ReminderDispatchWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<ReminderDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Edge case 1: On restart, immediately picks up
        // overdue pending reminders from the database.
        using var timer = new PeriodicTimer(PollInterval);

        // Process immediately on start, then on each tick
        do
        {
            try
            {
                await ProcessDueBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Reminder dispatch tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(
            stoppingToken));
    }

    private async Task ProcessDueBatchAsync(
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<IReminderDispatchRepository>();
        var dispatcher = scope.ServiceProvider
            .GetRequiredService<INotificationDispatcher>();

        var now = _timeProvider.GetUtcNow();
        var due = await repo.GetDueRemindersAsync(
            now, Tolerance, BatchSize, ct);

        if (due.Count == 0) return;

        _logger.LogInformation(
            "Processing {Count} due reminders", due.Count);

        foreach (var reminder in due)
        {
            // Edge case 2: Claim prevents duplicate dispatch
            var claimed = await repo
                .TryClaimForDispatchAsync(
                    reminder.ReminderId, ct);
            if (!claimed) continue;

            try
            {
                await dispatcher.DispatchAsync(reminder, ct);
                await repo.MarkSentAsync(
                    reminder.ReminderId,
                    _timeProvider.GetUtcNow(),
                    ct);

                _logger.LogInformation(
                    "Dispatched reminder {Id} via {Channel}",
                    reminder.ReminderId,
                    reminder.Channel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Dispatch failed for {Id}, will retry",
                    reminder.ReminderId);
                await repo.MarkRetryOrFailedAsync(
                    reminder.ReminderId, MaxRetries, ct);
            }
        }
    }
}
```

5. **Add composite index for dispatch query performance**:

```csharp
// In AppDbContext.OnModelCreating or ReminderEventConfiguration
// Add alongside existing configuration from task_001
entity.HasIndex(r => new { r.SendStatus, r.ScheduledAt })
      .HasDatabaseName(
        "IX_ReminderEvent_SendStatus_ScheduledAt")
      .HasFilter("\"SendStatus\" = 0"); // Pending only
```

6. **Register services in DI**:

```csharp
// In Program.cs or service registration extension
services.AddScoped<IReminderDispatchRepository,
    ReminderDispatchRepository>();
services.AddScoped<INotificationDispatcher,
    NotificationDispatcher>();
services.AddHostedService<ReminderDispatchWorker>();
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Program.cs                              (modify — register worker)
        ├── PropelIQ.Application/
        │   └── Reminders/
        │       ├── IReminderSchedulingService.cs        (existing from task_001)
        │       ├── ReminderSchedulingService.cs         (existing from task_001)
        │       ├── IReminderEventRepository.cs          (existing from task_001)
        │       ├── ReminderOffsets.cs                   (existing from task_001)
        │       ├── IReminderDispatchRepository.cs       (new)
        │       └── INotificationDispatcher.cs           (new)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── ReminderEvent.cs                     (existing from US_009)
        └── PropelIQ.Infrastructure/
            ├── Reminders/
            │   ├── ReminderEventRepository.cs           (existing from task_001)
            │   ├── BookingConfirmedReminderHandler.cs   (existing from task_001)
            │   ├── ReminderDispatchWorker.cs            (new)
            │   ├── ReminderDispatchRepository.cs        (new)
            │   └── NotificationDispatcher.cs            (new)
            └── Data/
                └── AppDbContext.cs                       (modify — dispatch index)
```

> Placeholder: Update on execution based on US_026 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Reminders/IReminderDispatchRepository.cs | Interface for querying due reminders, claiming, marking sent/failed |
| CREATE | server/src/PropelIQ.Infrastructure/Reminders/ReminderDispatchRepository.cs | EF Core implementation with optimistic claim and batch query |
| CREATE | server/src/PropelIQ.Application/Reminders/INotificationDispatcher.cs | Abstraction for email/SMS dispatch |
| CREATE | server/src/PropelIQ.Infrastructure/Reminders/NotificationDispatcher.cs | Polly-wrapped email/SMS dispatch with exponential backoff |
| CREATE | server/src/PropelIQ.Infrastructure/Reminders/ReminderDispatchWorker.cs | BackgroundService with 1-minute PeriodicTimer polling |
| MODIFY | server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs | Filtered composite index on (SendStatus, ScheduledAt) |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register ReminderDispatchWorker, dispatch repository, and notification dispatcher |

## External References

- PeriodicTimer (BCL): https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer
- BackgroundService: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- Polly v8 Resilience Pipelines: https://www.thepollyproject.org/2023/03/03/polly-v8-released/
- EF Core Filtered Indexes: https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-filter

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run backend (worker starts automatically)
dotnet run --project src/PropelIQ.Api

# Verify dispatch index migration
dotnet ef migrations add AddReminderDispatchIndex \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api
dotnet ef database update \
  --startup-project src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] Worker polls every 1 minute and picks up reminders within 1-minute tolerance (AC-2)
- [ ] Due reminders are dispatched via correct channel (email or SMS)
- [ ] Optimistic claim prevents duplicate dispatch across concurrent workers (edge case 2)
- [ ] Failed dispatch increments RetryCount and resets to Pending for retry
- [ ] Reminder transitions to Failed after 3 unsuccessful retry attempts
- [ ] Worker resumes from database state after restart — no reminders lost (edge case 1)
- [ ] Polly retry handles transient email/SMS provider failures with exponential backoff

## Implementation Checklist

- [ ] Create IReminderDispatchRepository with due query, claim, sent, and retry methods
- [ ] Implement ReminderDispatchRepository with optimistic Pending-to-Sending claim
- [ ] Create INotificationDispatcher abstraction and Polly-wrapped implementation
- [ ] Implement ReminderDispatchWorker with PeriodicTimer (1-minute interval) and batch processing
- [ ] Add filtered composite index on (SendStatus, ScheduledAt) for dispatch query
- [ ] Register all services and hosted worker in Program.cs DI container
- [ ] Implement max-retry (3) logic transitioning to Failed status on exhaustion
- [ ] Add structured logging for dispatch success, failure, and batch metrics

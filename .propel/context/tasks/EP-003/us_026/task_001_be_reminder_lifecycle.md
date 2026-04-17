# Task - TASK_001

## Requirement Reference

- User Story: us_026
- Story Location: .propel/context/tasks/EP-003/us_026/us_026.md
- Acceptance Criteria:
  - AC-1: Given a new appointment is confirmed, When the booking is persisted, Then the system schedules four reminder events: 7 days, 2 days, 1 day, and 2 hours before the appointment start time, using the patient's configured channels.
  - AC-3: Given an appointment is cancelled, When the cancellation is processed, Then all pending reminder events for that appointment are cancelled and no further reminders are sent.
  - AC-4: Given an appointment is rescheduled, When the new time is confirmed, Then existing reminder events are cancelled and new reminders are scheduled relative to the updated appointment time.
- Edge Cases:
  - What happens if the reminder scheduler restarts mid-schedule? Scheduled reminders are persisted in the database; on restart, the scheduler resumes from the next pending event.

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
| Library | FluentValidation | latest stable |
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

Implement the reminder event lifecycle management layer that reacts to booking domain events and persists `ReminderEvent` records in the database. When a booking is confirmed (AC-1), the `ReminderSchedulingService` creates four `ReminderEvent` rows at 7-day, 2-day, 1-day, and 2-hour offsets before the appointment start time, one per configured patient channel (email, SMS, or both per FR-RN-003). When a booking is cancelled (AC-3), all pending `ReminderEvent` records for that appointment are bulk-updated to `Cancelled` status in a single transaction. When a booking is rescheduled (AC-4), existing pending reminders are cancelled and new reminders are scheduled relative to the updated appointment time within the same transaction. Reminder events are persisted to the database (edge case — scheduler restart resilience), using `ReminderEvent.IdempotencyKey` (composite of `AppointmentId + Offset + Channel`) to prevent duplicate creation. The service subscribes to `BookingConfirmedEvent`, `BookingCancelledEvent`, and `BookingRescheduledEvent` via `System.Threading.Channels` (consistent with US_021 task_002 and US_022 task_001 patterns). Any reminder whose `ScheduledAt` is already in the past at creation time is skipped (e.g., booking made 1 day before appointment skips the 7-day and 2-day reminders). This task does not dispatch reminders — dispatch is handled by task_002.

## Dependent Tasks

- US_009 (Foundational — requires `ReminderEvent` entity and table in database)
- US_021 task_001 (requires `BookingConfirmedEvent` domain event and `Appointment` entity)
- US_022 task_001 (requires `BookingCancelledEvent` and `BookingRescheduledEvent` domain events)

## Impacted Components

- New: `server/src/PropelIQ.Application/Reminders/IReminderSchedulingService.cs` (interface)
- New: `server/src/PropelIQ.Application/Reminders/ReminderSchedulingService.cs` (create, cancel, reschedule reminder events)
- New: `server/src/PropelIQ.Application/Reminders/ReminderOffsets.cs` (static offsets: 7d, 2d, 1d, 2h)
- New: `server/src/PropelIQ.Infrastructure/Reminders/ReminderEventRepository.cs` (EF Core queries for ReminderEvent CRUD)
- New: `server/src/PropelIQ.Infrastructure/Reminders/BookingConfirmedReminderHandler.cs` (event handler — schedules reminders on confirmation)
- Modify: `server/src/PropelIQ.Infrastructure/Booking/BookingCancelledEventHandler.cs` (add call to cancel pending reminders)
- Modify: `server/src/PropelIQ.Infrastructure/Booking/BookingRescheduledEventHandler.cs` (add call to cancel + reschedule reminders)
- Modify: `server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs` (add `DbSet<ReminderEvent>` if not present from US_009)

## Implementation Plan

1. **Define reminder offset constants and idempotency key**:

```csharp
// server/src/PropelIQ.Application/Reminders/ReminderOffsets.cs
namespace PropelIQ.Application.Reminders;

public static class ReminderOffsets
{
    // FR-RN-001: 7d, 2d, 1d, 2h before appointment
    public static readonly TimeSpan[] All =
    [
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(1),
        TimeSpan.FromHours(2)
    ];

    /// <summary>
    /// Composite idempotency key prevents duplicate ReminderEvent creation
    /// on retries or duplicate event delivery.
    /// Format: {AppointmentId}_{OffsetMinutes}_{Channel}
    /// </summary>
    public static string BuildIdempotencyKey(
        Guid appointmentId,
        TimeSpan offset,
        string channel)
        => $"{appointmentId}_{(int)offset.TotalMinutes}_{channel}";
}
```

2. **Create `IReminderSchedulingService` interface and implementation**:

```csharp
// server/src/PropelIQ.Application/Reminders/IReminderSchedulingService.cs
namespace PropelIQ.Application.Reminders;

public interface IReminderSchedulingService
{
    // AC-1: Schedule 4 reminders per channel on booking confirmation
    Task ScheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset appointmentStart,
        Guid patientId,
        CancellationToken ct = default);

    // AC-3: Cancel all pending reminders on booking cancellation
    Task CancelRemindersAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    // AC-4: Cancel existing + schedule new on reschedule
    Task RescheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset newAppointmentStart,
        Guid patientId,
        CancellationToken ct = default);
}
```

```csharp
// server/src/PropelIQ.Application/Reminders/ReminderSchedulingService.cs
namespace PropelIQ.Application.Reminders;

public sealed class ReminderSchedulingService
    : IReminderSchedulingService
{
    private readonly IReminderEventRepository _repository;
    private readonly IPatientPreferenceRepository _preferences;
    private readonly TimeProvider _timeProvider;

    public ReminderSchedulingService(
        IReminderEventRepository repository,
        IPatientPreferenceRepository preferences,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _preferences = preferences;
        _timeProvider = timeProvider;
    }

    // AC-1
    public async Task ScheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset appointmentStart,
        Guid patientId,
        CancellationToken ct = default)
    {
        var channels = await _preferences
            .GetEnabledChannelsAsync(patientId, ct);
        var now = _timeProvider.GetUtcNow();
        var reminders = new List<ReminderEvent>();

        foreach (var offset in ReminderOffsets.All)
        {
            var scheduledAt = appointmentStart - offset;

            // Skip reminders whose time has already passed
            if (scheduledAt <= now) continue;

            foreach (var channel in channels)
            {
                var idempotencyKey = ReminderOffsets
                    .BuildIdempotencyKey(
                        appointmentId, offset, channel);

                reminders.Add(new ReminderEvent
                {
                    ReminderId = Guid.CreateVersion7(),
                    AppointmentId = appointmentId,
                    Channel = channel,
                    SendStatus = SendStatus.Pending,
                    ScheduledAt = scheduledAt,
                    RetryCount = 0,
                    IdempotencyKey = idempotencyKey
                });
            }
        }

        if (reminders.Count > 0)
        {
            await _repository.AddRangeAsync(reminders, ct);
        }
    }

    // AC-3
    public async Task CancelRemindersAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        await _repository.CancelPendingByAppointmentAsync(
            appointmentId, ct);
    }

    // AC-4
    public async Task RescheduleRemindersAsync(
        Guid appointmentId,
        DateTimeOffset newAppointmentStart,
        Guid patientId,
        CancellationToken ct = default)
    {
        await _repository.CancelPendingByAppointmentAsync(
            appointmentId, ct);

        await ScheduleRemindersAsync(
            appointmentId, newAppointmentStart, patientId, ct);
    }
}
```

3. **Create `IReminderEventRepository` and EF Core implementation**:

```csharp
// server/src/PropelIQ.Application/Reminders/IReminderEventRepository.cs
namespace PropelIQ.Application.Reminders;

public interface IReminderEventRepository
{
    Task AddRangeAsync(
        IEnumerable<ReminderEvent> events,
        CancellationToken ct = default);

    Task CancelPendingByAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default);
}
```

```csharp
// server/src/PropelIQ.Infrastructure/Reminders/ReminderEventRepository.cs
namespace PropelIQ.Infrastructure.Reminders;

public sealed class ReminderEventRepository
    : IReminderEventRepository
{
    private readonly AppDbContext _db;

    public ReminderEventRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(
        IEnumerable<ReminderEvent> events,
        CancellationToken ct = default)
    {
        // Upsert-safe: skip if idempotency key already exists
        foreach (var evt in events)
        {
            var exists = await _db.ReminderEvents
                .AnyAsync(
                    r => r.IdempotencyKey == evt.IdempotencyKey,
                    ct);
            if (!exists)
            {
                _db.ReminderEvents.Add(evt);
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    // AC-3: Bulk cancel pending reminders in single UPDATE
    public async Task CancelPendingByAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        await _db.ReminderEvents
            .Where(r =>
                r.AppointmentId == appointmentId
                && r.SendStatus == SendStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    r => r.SendStatus,
                    SendStatus.Cancelled),
                ct);
    }
}
```

4. **Create `BookingConfirmedReminderHandler`** — event handler consuming `BookingConfirmedEvent` via `System.Threading.Channels`:

```csharp
// server/src/PropelIQ.Infrastructure/Reminders/
//   BookingConfirmedReminderHandler.cs
namespace PropelIQ.Infrastructure.Reminders;

public sealed class BookingConfirmedReminderHandler
    : BackgroundService
{
    private readonly ChannelReader<BookingConfirmedEvent> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedReminderHandler> _logger;

    public BookingConfirmedReminderHandler(
        ChannelReader<BookingConfirmedEvent> reader,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedReminderHandler> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var evt in
            _reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IReminderSchedulingService>();

                await service.ScheduleRemindersAsync(
                    evt.AppointmentId,
                    evt.AppointmentStart,
                    evt.PatientId,
                    stoppingToken);

                _logger.LogInformation(
                    "Scheduled reminders for appointment {Id}",
                    evt.AppointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to schedule reminders for {Id}",
                    evt.AppointmentId);
            }
        }
    }
}
```

5. **Integrate cancellation and reschedule into existing event handlers**:

```csharp
// In BookingCancelledEventHandler.HandleAsync (modify existing)
// After existing cancellation logic:
var reminderService = scope.ServiceProvider
    .GetRequiredService<IReminderSchedulingService>();
await reminderService.CancelRemindersAsync(
    evt.AppointmentId, stoppingToken);
```

```csharp
// In BookingRescheduledEventHandler.HandleAsync (modify existing)
// After existing reschedule logic:
var reminderService = scope.ServiceProvider
    .GetRequiredService<IReminderSchedulingService>();
await reminderService.RescheduleRemindersAsync(
    evt.AppointmentId,
    evt.NewAppointmentStart,
    evt.PatientId,
    stoppingToken);
```

6. **Add unique index on `IdempotencyKey`** for duplicate prevention:

```csharp
// In AppDbContext.OnModelCreating or ReminderEventConfiguration
builder.Entity<ReminderEvent>(entity =>
{
    entity.HasKey(r => r.ReminderId);

    entity.HasIndex(r => r.IdempotencyKey)
          .IsUnique();

    entity.HasIndex(r => new
        { r.AppointmentId, r.SendStatus })
          .HasDatabaseName(
            "IX_ReminderEvent_AppointmentId_SendStatus");

    entity.HasOne<Appointment>()
          .WithMany()
          .HasForeignKey(r => r.AppointmentId)
          .OnDelete(DeleteBehavior.Cascade);
});
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/                        (no changes)
        ├── PropelIQ.Application/
        │   ├── Booking/
        │   │   ├── BookingService.cs               (existing)
        │   │   └── BookingConfirmedEvent.cs        (existing)
        │   └── Reminders/
        │       ├── IReminderSchedulingService.cs    (new)
        │       ├── ReminderSchedulingService.cs     (new)
        │       ├── IReminderEventRepository.cs      (new)
        │       └── ReminderOffsets.cs               (new)
        ├── PropelIQ.Domain/
        │   └── Entities/
        │       └── ReminderEvent.cs                 (existing from US_009)
        └── PropelIQ.Infrastructure/
            ├── Booking/
            │   ├── BookingCancelledEventHandler.cs  (modify)
            │   └── BookingRescheduledEventHandler.cs(modify)
            ├── Reminders/
            │   ├── ReminderEventRepository.cs       (new)
            │   └── BookingConfirmedReminderHandler.cs(new)
            └── Data/
                └── AppDbContext.cs                   (modify)
```

> Placeholder: Update on execution based on US_009, US_021 task_001, and US_022 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Reminders/IReminderSchedulingService.cs | Interface for schedule, cancel, reschedule reminder operations |
| CREATE | server/src/PropelIQ.Application/Reminders/ReminderSchedulingService.cs | Implementation with offset calculation, idempotency, and past-time skip |
| CREATE | server/src/PropelIQ.Application/Reminders/IReminderEventRepository.cs | Repository interface for ReminderEvent persistence |
| CREATE | server/src/PropelIQ.Application/Reminders/ReminderOffsets.cs | Static offsets (7d, 2d, 1d, 2h) and idempotency key builder |
| CREATE | server/src/PropelIQ.Infrastructure/Reminders/ReminderEventRepository.cs | EF Core repository with idempotent AddRange and bulk cancel |
| CREATE | server/src/PropelIQ.Infrastructure/Reminders/BookingConfirmedReminderHandler.cs | BackgroundService consuming BookingConfirmedEvent via Channel |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/BookingCancelledEventHandler.cs | Add CancelRemindersAsync call after cancellation logic |
| MODIFY | server/src/PropelIQ.Infrastructure/Booking/BookingRescheduledEventHandler.cs | Add RescheduleRemindersAsync call after reschedule logic |
| MODIFY | server/src/PropelIQ.Infrastructure/Data/AppDbContext.cs | Add unique index on IdempotencyKey, composite index on (AppointmentId, SendStatus) |

## External References

- EF Core ExecuteUpdateAsync: https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete
- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- TimeProvider (BCL abstraction): https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run backend
dotnet run --project src/PropelIQ.Api

# Verify ReminderEvent table and indexes
dotnet ef migrations add AddReminderEventIndexes \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api
dotnet ef database update \
  --startup-project src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] Four ReminderEvent rows created per channel on booking confirmation (AC-1)
- [ ] Reminders whose ScheduledAt is in the past are skipped at creation
- [ ] Cancelling a booking bulk-updates all pending reminders to Cancelled status (AC-3)
- [ ] Rescheduling cancels existing reminders and creates new ones for the updated time (AC-4)
- [ ] Duplicate ReminderEvent creation blocked by unique IdempotencyKey index (edge case 2 precondition)
- [ ] Scheduler restart has no effect — reminders persist in database and are not lost (edge case 1)
- [ ] BookingConfirmedReminderHandler logs errors without crashing on handler failure

## Implementation Checklist

- [ ] Define ReminderOffsets constants (7d, 2d, 1d, 2h) and BuildIdempotencyKey helper
- [ ] Create IReminderSchedulingService interface with schedule, cancel, and reschedule methods
- [ ] Implement ReminderSchedulingService with past-time skip and per-channel reminder creation
- [ ] Create IReminderEventRepository and EF Core implementation with idempotent AddRange
- [ ] Implement bulk CancelPendingByAppointmentAsync using ExecuteUpdateAsync
- [ ] Create BookingConfirmedReminderHandler BackgroundService consuming Channel
- [ ] Integrate CancelRemindersAsync into BookingCancelledEventHandler
- [ ] Integrate RescheduleRemindersAsync into BookingRescheduledEventHandler

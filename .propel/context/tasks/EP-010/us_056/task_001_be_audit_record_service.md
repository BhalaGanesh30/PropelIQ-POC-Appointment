# Task - TASK_001

## Requirement Reference

- User Story: us_056
- Story Location: .propel/context/tasks/EP-010/us_056/us_056.md
- Acceptance Criteria:
  - AC-1: Given any authentication, data access, override, configuration change, or coding review event occurs, When the event is processed, Then an AuditRecord is written to the append-only audit table within 1 second with actor identity, action type, affected resource, timestamp, and structured detail payload.
  - AC-2: Given an AuditRecord is written, When any role (including DBA) attempts to UPDATE or DELETE it, Then the operation is rejected by the database write-restriction policy and the attempt is itself logged.
  - AC-4: Given an admin accesses the audit log viewer, When they query with filters (actor, action type, date range, resource ID), Then matching records are returned with pagination within 3 seconds.
- Edge Cases:
  - What happens if the audit table write fails during a transaction? The business transaction is rolled back; a dead-letter audit event is written to a separate fallback queue for retry; no silent data loss occurs.
  - How does the system handle export of large audit log batches for external compliance review? Async export is triggered; when complete, the file is delivered via secure download link to the requesting admin.

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
| Library | EF Core | 8.x |
| Library | System.Threading.Channels | 8.x |
| Library | Polly | 8.x |
| Library | FluentValidation | 11.x |
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

Implement the application-layer audit record writing service and admin query API for the immutable audit trail. An `IAuditRecordService` interface with `WriteAsync(AuditEvent)` provides the central contract consumed by all modules (authentication, RBAC, booking, override, coding review) to emit audit events. The service writes events through a `System.Threading.Channels` bounded channel to an `AuditRecordWriterWorker` background service that persists `AuditRecord` entities to the append-only `app.audit_records` table (US_010 foundation) within 1 second of event occurrence (AC-1). If the database write fails, the event is routed to a dead-letter `audit_dead_letter` table with retry metadata; a `DeadLetterRetryWorker` retries failed writes with Polly exponential backoff (edge case 1). When a business transaction includes audit writing and the audit INSERT fails, the transaction is rolled back and the event is captured in the dead-letter queue to prevent silent data loss. Rejected UPDATE/DELETE attempts on the audit table are already blocked by US_010's trigger; this task ensures the application layer logs the `DbUpdateException` containing the trigger rejection message at Warning level with structured fields for compliance traceability (AC-2). A `GET /api/v1/admin/audit-logs` endpoint restricted to Admin role returns filtered, paginated audit records within 3 seconds (AC-4). Filters include actor (user ID or name), action type (enum), date range (from/to UTC), and resource ID. A `POST /api/v1/admin/audit-logs/export` endpoint triggers an asynchronous CSV export job; on completion the admin receives a secure, time-limited download link via the response or notification (edge case 2).

## Dependent Tasks

- US_010 task_001 (requires append-only `audit_records` table, immutability trigger, and pgaudit configuration)
- US_010 task_002 (requires transactional integrity infrastructure)
- US_015 task_001 (requires RBAC policies for Admin role authorization)

## Impacted Components

- New: `server/src/PropelIQ.Application/Audit/IAuditRecordService.cs` (service contract)
- New: `server/src/PropelIQ.Application/Audit/AuditEvent.cs` (event DTO)
- New: `server/src/PropelIQ.Infrastructure/Audit/AuditRecordService.cs` (channel-based writer)
- New: `server/src/PropelIQ.Infrastructure/Audit/AuditRecordWriterWorker.cs` (BackgroundService consumer)
- New: `server/src/PropelIQ.Infrastructure/Audit/DeadLetterRetryWorker.cs` (retry worker for failed writes)
- New: `server/src/PropelIQ.Api/Controllers/Admin/AuditLogController.cs` (GET query + POST export)
- New: `server/src/PropelIQ.Application/Audit/AuditLogQueryRequest.cs` (filter/pagination DTO)
- New: `server/src/PropelIQ.Application/Audit/AuditLogExportService.cs` (async CSV export)
- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/<timestamp>_AuditDeadLetterTable.cs` (dead-letter table migration)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register audit services and background workers)

## Implementation Plan

1. **Define the `AuditEvent` DTO and `IAuditRecordService` contract** in the Application layer. The event captures all fields required by AC-1:

```csharp
// server/src/PropelIQ.Application/Audit/AuditEvent.cs
namespace PropelIQ.Application.Audit;

public sealed record AuditEvent
{
    public required Guid UserId { get; init; }
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required Dictionary<string, object> Details
        { get; init; }
}

// IAuditRecordService.cs
public interface IAuditRecordService
{
    ValueTask WriteAsync(
        AuditEvent auditEvent,
        CancellationToken ct = default);
}
```

The `WriteAsync` method posts the event to a bounded channel for asynchronous persistence. The channel capacity is set to 10,000 with `BoundedChannelFullMode.Wait` to apply back-pressure rather than dropping events.

2. **Implement `AuditRecordService`** that writes to a `System.Threading.Channels` bounded channel:

```csharp
// server/src/PropelIQ.Infrastructure/Audit/
//   AuditRecordService.cs
public sealed class AuditRecordService
    : IAuditRecordService
{
    private readonly Channel<AuditEvent> _channel;

    public AuditRecordService(
        Channel<AuditEvent> channel)
    {
        _channel = channel;
    }

    public async ValueTask WriteAsync(
        AuditEvent auditEvent,
        CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(
            auditEvent, ct);
    }
}
```

The channel decouples the caller from the database write, keeping the audit write latency sub-millisecond at the call site. The 1-second AC-1 target is measured end-to-end from channel write to database INSERT completion.

3. **Implement `AuditRecordWriterWorker`** as a `BackgroundService` that reads from the channel and persists to the database:

```csharp
// server/src/PropelIQ.Infrastructure/Audit/
//   AuditRecordWriterWorker.cs
public sealed class AuditRecordWriterWorker
    : BackgroundService
{
    private readonly Channel<AuditEvent> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRecordWriterWorker> _log;

    protected override async Task ExecuteAsync(
        CancellationToken ct)
    {
        await foreach (var evt in
            _channel.Reader.ReadAllAsync(ct))
        {
            using var scope =
                _scopeFactory.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var record = new AuditRecord
            {
                AuditId = Guid.CreateVersion7(),
                UserId = evt.UserId,
                EventType = evt.EventType,
                EntityType = evt.EntityType,
                EntityId = evt.EntityId,
                Details = JsonSerializer
                    .Serialize(evt.Details),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                db.AuditRecords.Add(record);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // AC-2: Log trigger rejection
                _log.LogWarning(ex,
                    "Audit write rejected for " +
                    "{EventType} by {UserId}. " +
                    "Routing to dead-letter.",
                    evt.EventType, evt.UserId);

                await WriteToDeadLetter(
                    db, evt, ex.Message, ct);
            }
        }
    }

    private static async Task WriteToDeadLetter(
        AppDbContext db,
        AuditEvent evt,
        string errorMessage,
        CancellationToken ct)
    {
        db.AuditDeadLetters.Add(
            new AuditDeadLetter
            {
                Id = Guid.CreateVersion7(),
                Payload = JsonSerializer
                    .Serialize(evt),
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            });
        await db.SaveChangesAsync(ct);
    }
}
```

The worker processes events sequentially to maintain ordering guarantees. Batch processing can be added later if throughput requires it.

4. **Create the dead-letter table migration** for failed audit writes (edge case 1):

```sql
-- In EF Core migration via migrationBuilder.Sql()
CREATE TABLE IF NOT EXISTS app.audit_dead_letters (
    id UUID PRIMARY KEY,
    payload JSONB NOT NULL,
    error_message TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    retry_count INT NOT NULL DEFAULT 0,
    last_retry_at TIMESTAMPTZ,
    resolved_at TIMESTAMPTZ
);

CREATE INDEX ix_audit_dead_letters_unresolved
    ON app.audit_dead_letters (created_at)
    WHERE resolved_at IS NULL;
```

5. **Implement `DeadLetterRetryWorker`** that periodically retries unresolved dead-letter entries with Polly exponential backoff:

```csharp
// server/src/PropelIQ.Infrastructure/Audit/
//   DeadLetterRetryWorker.cs
public sealed class DeadLetterRetryWorker
    : BackgroundService
{
    // Runs every 5 minutes
    // Selects unresolved dead-letter entries
    //   WHERE retry_count < 5
    //   AND resolved_at IS NULL
    // Attempts re-INSERT into audit_records
    // On success: sets resolved_at = now()
    // On failure: increments retry_count,
    //   sets last_retry_at = now()
    // After 5 retries: raises compliance alert
    //   via ILogger at Critical level
}
```

6. **Implement `AuditLogController`** with query and export endpoints:

```csharp
// server/src/PropelIQ.Api/Controllers/Admin/
//   AuditLogController.cs
[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = "AdminOnly")]
public sealed class AuditLogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogExportService _export;

    // AC-4: GET with filters and pagination
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] AuditLogQueryRequest request,
        CancellationToken ct)
    {
        var query = _db.AuditRecords.AsQueryable();

        if (request.ActorId.HasValue)
            query = query.Where(
                r => r.UserId == request.ActorId);
        if (!string.IsNullOrEmpty(request.ActionType))
            query = query.Where(
                r => r.EventType == request.ActionType);
        if (request.FromUtc.HasValue)
            query = query.Where(
                r => r.CreatedAt >= request.FromUtc);
        if (request.ToUtc.HasValue)
            query = query.Where(
                r => r.CreatedAt <= request.ToUtc);
        if (request.ResourceId.HasValue)
            query = query.Where(
                r => r.EntityId == request.ResourceId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return Ok(new { total, items });
    }

    // Edge case 2: Async export with secure download
    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromBody] AuditLogQueryRequest request,
        CancellationToken ct)
    {
        var jobId = await _export
            .EnqueueExportAsync(request, ct);
        return Accepted(new { jobId });
    }

    [HttpGet("export/{jobId}")]
    public async Task<IActionResult> DownloadExport(
        Guid jobId, CancellationToken ct)
    {
        var result = await _export
            .GetExportAsync(jobId, ct);
        if (result is null)
            return NotFound();
        if (!result.IsComplete)
            return Accepted(new
            {
                status = "Processing"
            });

        return File(
            result.FileStream,
            "text/csv",
            result.FileName);
    }
}
```

The query endpoint uses EF Core LINQ with server-side filtering and pagination. An index on `(created_at, event_type)` ensures the 3-second response target (AC-4) is met even against large datasets. The export endpoint returns HTTP 202 Accepted with a job ID; the admin polls or receives notification when the CSV is ready.

7. **Implement `AuditLogExportService`** for async CSV generation (edge case 2):

```csharp
// server/src/PropelIQ.Application/Audit/
//   AuditLogExportService.cs
public sealed class AuditLogExportService
{
    // EnqueueExportAsync: Creates export job record,
    //   enqueues background work
    // Background worker: Streams query results to CSV
    //   using CsvHelper, stores to local temp directory
    //   with time-limited access (1 hour expiry)
    // GetExportAsync: Returns file stream if complete,
    //   null if not found, or processing status
    // Secure download: File path is never exposed;
    //   download requires Admin auth + valid jobId
}
```

8. **Register services in `Program.cs`**:

```csharp
// Channel registration
builder.Services.AddSingleton(
    Channel.CreateBounded<AuditEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        }));

builder.Services
    .AddSingleton<IAuditRecordService,
        AuditRecordService>();
builder.Services
    .AddHostedService<AuditRecordWriterWorker>();
builder.Services
    .AddHostedService<DeadLetterRetryWorker>();
builder.Services
    .AddScoped<AuditLogExportService>();
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       ├── PropelIQ.Api/
│       │   ├── Program.cs                             (modify)
│       │   └── Controllers/
│       │       └── Admin/
│       │           └── AuditLogController.cs          (new)
│       ├── PropelIQ.Application/
│       │   └── Audit/
│       │       ├── IAuditRecordService.cs             (new)
│       │       ├── AuditEvent.cs                      (new)
│       │       ├── AuditLogQueryRequest.cs            (new)
│       │       └── AuditLogExportService.cs           (new)
│       ├── PropelIQ.Domain/
│       │   └── Entities/
│       │       ├── AuditRecord.cs                     (from US_009)
│       │       └── AuditDeadLetter.cs                 (new)
│       └── PropelIQ.Infrastructure/
│           ├── Audit/
│           │   ├── AuditRecordService.cs              (new)
│           │   ├── AuditRecordWriterWorker.cs         (new)
│           │   └── DeadLetterRetryWorker.cs           (new)
│           └── Persistence/
│               ├── AppDbContext.cs                     (modify)
│               └── Migrations/
│                   └── <timestamp>_AuditDeadLetterTable.cs (new)
└── docker-compose.yml
```

> Placeholder: Update on execution based on US_010 and US_015 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Audit/IAuditRecordService.cs | Service contract with WriteAsync for audit event emission |
| CREATE | server/src/PropelIQ.Application/Audit/AuditEvent.cs | Event DTO with UserId, EventType, EntityType, EntityId, Details |
| CREATE | server/src/PropelIQ.Application/Audit/AuditLogQueryRequest.cs | Filter/pagination DTO for admin query endpoint |
| CREATE | server/src/PropelIQ.Application/Audit/AuditLogExportService.cs | Async CSV export job management |
| CREATE | server/src/PropelIQ.Domain/Entities/AuditDeadLetter.cs | Dead-letter entity for failed audit writes |
| CREATE | server/src/PropelIQ.Infrastructure/Audit/AuditRecordService.cs | Channel-based implementation posting events to bounded channel |
| CREATE | server/src/PropelIQ.Infrastructure/Audit/AuditRecordWriterWorker.cs | BackgroundService consuming channel and persisting to database |
| CREATE | server/src/PropelIQ.Infrastructure/Audit/DeadLetterRetryWorker.cs | Periodic retry worker for unresolved dead-letter entries |
| CREATE | server/src/PropelIQ.Api/Controllers/Admin/AuditLogController.cs | GET query with filters/pagination and POST export endpoints |
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Migrations/\<timestamp\>_AuditDeadLetterTable.cs | Dead-letter table with filtered index on unresolved entries |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register channel, audit services, and background workers |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add DbSet for AuditDeadLetter |

## External References

- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- ASP.NET Core BackgroundService: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0
- EF Core querying: https://learn.microsoft.com/en-us/ef/core/querying/
- Polly retry policies: https://www.thepollyproject.org/
- ASP.NET Core authorization policies: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-8.0
- PostgreSQL JSONB: https://www.postgresql.org/docs/15/datatype-json.html
- CsvHelper library: https://joshclose.github.io/CsvHelper/

## Build Commands

```bash
# Add dead-letter migration
dotnet ef migrations add AuditDeadLetterTable \
  --project server/src/PropelIQ.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update \
  --project server/src/PropelIQ.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Build backend
dotnet build server/PropelIQ.sln

# Run API
dotnet run --project server/src/PropelIQ.Api
```

## Implementation Validation Strategy

- [x] AuditRecord is written within 1 second of event emission via channel (AC-1)
- [x] AuditRecord contains actor identity, action type, affected resource, timestamp, and structured detail payload (AC-1)
- [x] UPDATE/DELETE rejection by trigger is caught and logged at Warning level with structured fields (AC-2)
- [x] Failed audit writes are routed to dead-letter table with retry metadata (edge case 1)
- [x] Dead-letter retry worker re-attempts failed writes with exponential backoff
- [x] Admin-only GET endpoint returns filtered, paginated results within 3 seconds (AC-4)
- [x] Async export returns 202 Accepted and generates downloadable CSV (edge case 2)
- [x] Export download requires Admin auth and valid job ID (secure access)

## Implementation Checklist

- [x] Define `AuditEvent` DTO and `IAuditRecordService` contract in Application layer
- [x] Implement `AuditRecordService` with bounded channel writer (capacity 10,000)
- [x] Implement `AuditRecordWriterWorker` BackgroundService consuming channel events
- [x] Create dead-letter table migration with filtered index on unresolved entries
- [x] Implement `DeadLetterRetryWorker` with Polly exponential backoff (max 5 retries)
- [x] Implement `AuditLogController` with GET query (filters + pagination) and POST export endpoints
- [x] Implement `AuditLogExportService` for async CSV generation with time-limited download
- [x] Register channel, services, and background workers in `Program.cs`

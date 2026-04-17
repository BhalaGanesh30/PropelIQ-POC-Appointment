---
task_id: task_002
user_story: us_031
epic: EP-004
layer: Backend
status: not-started
effort_hours: 6
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_031] Real-Time Queue Dashboard
- **Story Location**: [.propel/context/tasks/EP-004/us_031/us_031.md](.propel/context/tasks/EP-004/us_031/us_031.md)
- **Acceptance Criteria**:
  - AC-1: All today's appointments returned with status badges (Waiting, In-Progress, Completed, No-Show) and wait-time estimates; response within NFR-002 (500ms p95).
  - AC-2: Status-specific filtering supported via query parameter.
  - AC-3: `isOverdue` flag returned per entry so the frontend can highlight overdue rows.
  - AC-4: Endpoint protected by role-based authorization (Staff/Admin only).
- **Edge Cases**:
  - Edge Case 1: If Redis cache miss occurs, fall through to PostgreSQL without surfacing errors to the client; refresh cache on each miss.
  - Edge Case 2: Invalid `status` filter value → return `HTTP 400` with a validation error body; do not query the database.

---

## Design References (Backend Task)

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

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
| Cache | Redis (StackExchange.Redis) | 2.x |
| Auth | ASP.NET Core Identity + JWT | 8.x |
| Observability | OpenTelemetry .NET | 1.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the `QueueController` and `QueueService` in the `Scheduling` module of the ASP.NET Core 8 Web API. Expose `GET /api/v1/queue/today` which returns all appointments for the current calendar day enriched with queue state, patient name, appointment type, wait-time estimate, and an `isOverdue` flag computed by `IWaitTimeEstimationService` (task_003). Responses are cached in Redis with a 15-second TTL (keyed by clinic and optional status filter) to meet NFR-002 (500ms p95). The endpoint is secured with `[Authorize(Roles = "Staff,Admin")]` and accepts an optional `status` query parameter validated against the `QueueState` enum whitelist.

---

## Dependent Tasks

- **task_003** — `IWaitTimeEstimationService` must be implemented (or stubbed via interface) before `QueueService` can compute `estimatedWaitMinutes` and `isOverdue`.
- **task_004** — DB migration must be applied so `QueueState`, `ArrivedAt`, `VisitStartedAt`, `VisitEndedAt` columns exist on `Appointments`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `QueueController` | CREATE | New controller in `Scheduling` module |
| `QueueService` | CREATE | Business logic aggregating today's queue |
| `QueueEntryDto` | CREATE | Response DTO per patient entry |
| `QueueResponseDto` | CREATE | Wrapper DTO with entries list and `generatedAt` timestamp |
| `QueueState` (enum) | CREATE | `Scheduled | Arrived | InProgress | Completed | NoShow` — shared with DB model |
| `IQueueService` | CREATE | Interface for DI and testability |
| `SchedulingModule` DI registration | MODIFY | Register `QueueService`, `IQueueService` as scoped |
| `Program.cs` / `appsettings.json` | MODIFY | Redis connection string; cache TTL config (`Queue:CacheTtlSeconds`, default `15`) |

---

## Implementation Plan

1. **Create `QueueState` enum** in `Scheduling/Domain/Enums/QueueState.cs`: `Scheduled = 0, Arrived = 1, InProgress = 2, Completed = 3, NoShow = 4`.
2. **Create DTOs**: `QueueEntryDto` (`PatientId`, `PatientName`, `AppointmentType`, `Status: QueueState`, `ArrivedAt: DateTimeOffset?`, `EstimatedWaitMinutes: int`, `IsOverdue: bool`) and `QueueResponseDto` (`Entries: IReadOnlyList<QueueEntryDto>`, `GeneratedAt: DateTimeOffset`, `TotalCount: int`).
3. **Create `IQueueService`** interface with `GetTodayQueueAsync(QueueState? statusFilter, CancellationToken ct)` returning `Task<QueueResponseDto>`.
4. **Implement `QueueService`**:
   - Query `Appointments` table filtered by `AppointmentDate == DateTime.UtcNow.Date` and optional `QueueState == statusFilter`.
   - Join with `Patients` for `PatientName`.
   - For each entry, call `IWaitTimeEstimationService.CalculateEstimatedWaitMinutes` and `IsOverdue`.
   - Return a sorted `QueueResponseDto` ordered by `ArrivedAt ASC, AppointmentTime ASC`.
5. **Implement Redis caching** in `QueueService`: attempt `IDistributedCache.GetStringAsync("queue:today:{clinicId}:{statusFilter}")` before DB query; on miss, compute and store result with `AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CacheTtlSeconds)`.
6. **Create `QueueController`**: route `GET /api/v1/queue/today`, bind `[FromQuery] QueueState? status`, apply `[Authorize(Roles = "Staff,Admin")]`, delegate to `IQueueService.GetTodayQueueAsync`, return `Ok(result)`.
7. **Add input validation**: Use a `[FromQuery]` binding guard — if an unrecognised string is provided for `status`, ASP.NET model binding returns `HTTP 400` automatically via `[ApiController]`; document this in OpenAPI XML comment.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Controllers/
│   │   │   └── QueueController.cs          ← CREATE
│   │   ├── Services/
│   │   │   ├── IQueueService.cs            ← CREATE
│   │   │   └── QueueService.cs             ← CREATE
│   │   ├── Domain/
│   │   │   └── Enums/
│   │   │       └── QueueState.cs           ← CREATE
│   │   └── DTOs/
│   │       ├── QueueEntryDto.cs            ← CREATE
│   │       └── QueueResponseDto.cs         ← CREATE
│   └── [existing modules...]
└── Program.cs                              ← MODIFY (DI registration)
```

> Placeholder: Update this tree after task_004 (migration) is complete and the actual `Scheduling` module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Controllers/QueueController.cs` | `GET /api/v1/queue/today` endpoint with `[Authorize(Roles="Staff,Admin")]` |
| CREATE | `Server/Modules/Scheduling/Services/IQueueService.cs` | Interface for queue aggregation service |
| CREATE | `Server/Modules/Scheduling/Services/QueueService.cs` | EF Core query + Redis caching + DTO projection |
| CREATE | `Server/Modules/Scheduling/Domain/Enums/QueueState.cs` | `QueueState` enum shared by domain and API |
| CREATE | `Server/Modules/Scheduling/DTOs/QueueEntryDto.cs` | Per-patient response DTO |
| CREATE | `Server/Modules/Scheduling/DTOs/QueueResponseDto.cs` | Wrapper with entry list and metadata |
| MODIFY | `Server/Program.cs` | Register `IQueueService → QueueService` as scoped |
| MODIFY | `Server/appsettings.json` | Add `"Queue": { "CacheTtlSeconds": 15 }` |

---

## External References

- ASP.NET Core 8 `IDistributedCache` with Redis: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0
- EF Core 8 query filtering and projection: https://learn.microsoft.com/en-us/ef/core/querying/
- ASP.NET Core 8 `[Authorize]` role-based access: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- ASP.NET Core 8 `[ApiController]` automatic model validation (HTTP 400): https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#automatic-http-400-responses
- OpenTelemetry .NET Activity API: https://opentelemetry.io/docs/languages/net/instrumentation/
- NFR-002: Queue API response ≤ 500ms p95 — enforced by Redis caching (15s TTL) + indexed DB query (see task_004)

---

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run API locally
dotnet run --project Server/Server.csproj

# Run tests
dotnet test

# EF Core migrations (applied in task_004 — do not run here)
# dotnet ef migrations add QueueStateFields
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `QueueService` (mock `IWaitTimeEstimationService`, mock `IDistributedCache`, mock EF context)
- [ ] Integration tests pass — `GET /api/v1/queue/today` returns `HTTP 200` with correct shape for authenticated Staff role
- [ ] `GET /api/v1/queue/today?status=InvalidValue` returns `HTTP 400`
- [ ] Unauthenticated request returns `HTTP 401`; Patient role returns `HTTP 403`
- [ ] Redis cache key `queue:today:{clinicId}:*` is populated after first request; second request within 15s is served from cache (verified via Redis CLI `GET`)
- [ ] Response time ≤ 500ms p95 under 100 concurrent requests (verify via load test or stopwatch log)
- [ ] `isOverdue: true` returned for appointments where `arrivedAt` + `estimatedWaitMinutes` < current time
- [ ] OpenTelemetry span `queue.fetch.today` appears in trace output

---

## Implementation Checklist

- [ ] Create `QueueState` enum in `Scheduling/Domain/Enums/QueueState.cs`
- [ ] Create `QueueEntryDto` and `QueueResponseDto` in `Scheduling/DTOs/`
- [ ] Create `IQueueService` interface with `GetTodayQueueAsync` method signature
- [ ] Implement `QueueService` with EF Core today-filter query, patient join, and DTO projection
- [ ] Integrate `IWaitTimeEstimationService` call per entry for `estimatedWaitMinutes` and `isOverdue`
- [ ] Implement Redis cache check/store in `QueueService` using `IDistributedCache` with configurable TTL
- [ ] Create `QueueController` with `[Authorize(Roles="Staff,Admin")]` and `GET /api/v1/queue/today` action
- [ ] Register `IQueueService → QueueService` as scoped in `Program.cs`; add `Queue:CacheTtlSeconds` to `appsettings.json`

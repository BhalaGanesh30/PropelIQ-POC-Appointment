---
task_id: task_002
user_story: us_036
epic: EP-004
layer: Backend
status: not-started
effort_hours: 6
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_036] Daily Schedule Calendar with Drag-and-Drop
- **Story Location**: [.propel/context/tasks/EP-004/us_036/us_036.md](.propel/context/tasks/EP-004/us_036/us_036.md)
- **Acceptance Criteria**:
  - AC-1: All appointments for the selected date are returned with patient names, types, and durations for calendar display.
  - AC-2: Reschedule endpoint updates the appointment time, validates conflicts, and creates an audit record with the override reason.
  - AC-4: Schedule data for a date is served within NFR-002 (500ms p95) via Redis caching.
- **Edge Cases**:
  - Edge Case 1: Reschedule to an occupied time slot; API returns `409 Conflict` with conflicting appointment details.
  - Edge Case 2: Day with no appointments; API returns an empty array.

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

Implement the daily schedule retrieval and drag-and-drop reschedule API endpoints in the `Scheduling` module of the ASP.NET Core 8 Web API. This task exposes `GET /api/v1/schedule/daily?date={yyyy-MM-dd}` which returns all appointments for a given date with patient names, appointment types, start times, and durations — cached in Redis with a 30-second TTL to meet the sub-1-second frontend load target and NFR-002 (500ms p95). Additionally, expose `PUT /api/v1/schedule/reschedule` which updates the appointment time after validating that no time conflict exists at the target slot. If a conflict is detected, the endpoint returns `409 Conflict` with the conflicting appointment details. On successful reschedule, the endpoint creates an immutable audit record via `IAuditService` capturing the override reason, old time, new time, and staff identity. The Redis cache for the affected date is invalidated after the reschedule. Both endpoints are secured with `[Authorize(Roles = "Staff,Admin")]`.

---

## Dependent Tasks

- **us_031/task_002** — `QueueController` and `QueueService` must exist; reschedule invalidates queue cache.
- **us_031/task_004** — Queue state migration must be applied so appointment time and status columns exist.
- **us_034/task_002** — `SchedulingOverrideService` and override audit infrastructure are reused for the override reason audit trail.
- **us_034/task_003** — Override audit columns on `audit_records` must exist for override reason persistence.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ScheduleController` | CREATE | New controller: `GET /api/v1/schedule/daily`, `PUT /api/v1/schedule/reschedule` |
| `IScheduleService` | CREATE | Interface for daily schedule retrieval and reschedule |
| `ScheduleService` | CREATE | Business logic: date-filtered query, conflict detection, reschedule with audit |
| `DailyScheduleEntryDto` | CREATE | Response DTO: `AppointmentId`, `PatientName`, `AppointmentType`, `StartTime`, `DurationMinutes`, `Status` |
| `DailyScheduleResponseDto` | CREATE | Wrapper DTO: `Entries`, `Date`, `TotalCount` |
| `RescheduleRequest` DTO | CREATE | Request: `AppointmentId`, `NewStartTime`, `OverrideReason` |
| `RescheduleResponse` DTO | CREATE | Response: `AppointmentId`, `OldStartTime`, `NewStartTime`, `AuditRecordId` |
| `AppointmentService` | MODIFY | Add `RescheduleAsync` method that updates appointment time with conflict validation |
| `SchedulingModule` DI registration | MODIFY | Register `ScheduleService` as scoped |

---

## Implementation Plan

1. **Create DTOs** in `Scheduling/DTOs/`: `DailyScheduleEntryDto` with `Guid AppointmentId`, `string PatientName`, `string AppointmentType`, `DateTimeOffset StartTime`, `int DurationMinutes`, `string Status`. `DailyScheduleResponseDto` wrapping entries list with `DateOnly Date` and `int TotalCount`. `RescheduleRequest` with `[Required] Guid AppointmentId`, `[Required] DateTimeOffset NewStartTime`, `[Required, MaxLength(500), MinLength(1)] string OverrideReason`. `RescheduleResponse` with old/new times and audit record ID.
2. **Create `IScheduleService`** interface with `GetDailyScheduleAsync(DateOnly date, CancellationToken ct)` returning `Task<DailyScheduleResponseDto>` and `RescheduleAsync(RescheduleRequest request, Guid staffUserId, CancellationToken ct)` returning `Task<RescheduleResponse>`.
3. **Implement `ScheduleService.GetDailyScheduleAsync`**:
   - Attempt Redis `IDistributedCache.GetStringAsync("schedule:daily:{date}")`.
   - On cache miss, query `Appointments` table filtered by `date_time::date = @date`, join with `Patients` for `PatientName`, project to `DailyScheduleEntryDto`, order by `StartTime ASC`.
   - Store result in Redis with `AbsoluteExpirationRelativeToNow = 30 seconds`.
   - Return `DailyScheduleResponseDto`.
4. **Implement `ScheduleService.RescheduleAsync`**:
   - Load the appointment by ID (return `404` if not found).
   - Detect conflict: query `Appointments` for the same date where `date_time` overlaps the new time range (start to start + duration). Exclude the current appointment from the conflict check. If conflict found, return `409 Conflict` with `ConflictCheckResponse` containing conflicting appointment details.
   - Update `Appointment.DateTime = NewStartTime` within a transaction.
   - Create audit record via `IAuditService.LogOverrideAsync` with `EventType = ScheduleOverride`, staff identity, old time, new time, and override reason.
   - Invalidate Redis cache keys: `schedule:daily:{date}` (old date) and `schedule:daily:{newDate}` (if date changed), plus `queue:today:*`.
   - Return `RescheduleResponse`.
5. **Create `ScheduleController`** at route `api/v1/schedule`: `GET /daily` accepts `[FromQuery] DateOnly date`, applies `[Authorize(Roles = "Staff,Admin")]`. `PUT /reschedule` accepts `[FromBody] RescheduleRequest`, applies same authorization. Extract `staffUserId` from JWT claims.
6. **Register DI**: Add `IScheduleService` → `ScheduleService` as scoped in `Program.cs`.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Controllers/
│   │   │   ├── QueueController.cs                     ← EXISTS (us_031)
│   │   │   ├── WalkinController.cs                    ← EXISTS (us_033)
│   │   │   ├── SchedulingOverrideController.cs        ← EXISTS (us_034)
│   │   │   ├── StaffBookingController.cs              ← EXISTS (us_035)
│   │   │   └── ScheduleController.cs                  ← CREATE
│   │   ├── Services/
│   │   │   ├── IScheduleService.cs                    ← CREATE
│   │   │   ├── ScheduleService.cs                     ← CREATE
│   │   │   ├── AppointmentService.cs                  ← MODIFY
│   │   │   └── [existing services...]
│   │   └── DTOs/
│   │       ├── DailyScheduleEntryDto.cs               ← CREATE
│   │       ├── DailyScheduleResponseDto.cs            ← CREATE
│   │       ├── RescheduleRequest.cs                   ← CREATE
│   │       └── RescheduleResponse.cs                  ← CREATE
│   └── [existing modules...]
├── Program.cs                                          ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Controllers/ScheduleController.cs` | `GET /api/v1/schedule/daily` and `PUT /api/v1/schedule/reschedule` with `[Authorize(Roles="Staff,Admin")]` |
| CREATE | `Server/Modules/Scheduling/Services/IScheduleService.cs` | Interface for daily schedule and reschedule operations |
| CREATE | `Server/Modules/Scheduling/Services/ScheduleService.cs` | EF Core query + Redis caching + conflict detection + audit logging |
| CREATE | `Server/Modules/Scheduling/DTOs/DailyScheduleEntryDto.cs` | Per-appointment response DTO |
| CREATE | `Server/Modules/Scheduling/DTOs/DailyScheduleResponseDto.cs` | Wrapper with entries, date, and count |
| CREATE | `Server/Modules/Scheduling/DTOs/RescheduleRequest.cs` | Request DTO with `[Required]` validation on reason |
| CREATE | `Server/Modules/Scheduling/DTOs/RescheduleResponse.cs` | Response DTO with old/new times and audit record ID |
| MODIFY | `Server/Modules/Scheduling/Services/AppointmentService.cs` | Add `RescheduleAsync` method for time update with conflict validation |
| MODIFY | `Server/Program.cs` | Register `IScheduleService` as scoped |

---

## External References

- ASP.NET Core 8 `IDistributedCache` with Redis: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0
- EF Core 8 query filtering and projection: https://learn.microsoft.com/en-us/ef/core/querying/
- EF Core 8 transactions: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- ASP.NET Core 8 role-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- ASP.NET Core 8 `DateOnly` model binding: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-8.0
- FR-SO-006: Daily schedule views with drag-and-drop rearrangement and print-friendly rendering
- NFR-002: API response within 500ms p95 — enforced by Redis caching (30s TTL) + indexed DB query

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
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `ScheduleService.GetDailyScheduleAsync` (mock EF context, mock `IDistributedCache`)
- [ ] Unit tests pass for `ScheduleService.RescheduleAsync` (mock EF context, mock `IAuditService`, mock cache)
- [ ] Integration tests pass for `GET /api/v1/schedule/daily?date=2026-04-17` returning all appointments sorted by time
- [ ] Integration tests pass for `PUT /api/v1/schedule/reschedule` returning `200` with updated times
- [ ] Integration tests pass for `PUT /api/v1/schedule/reschedule` returning `409` on time conflict
- [ ] Authorization verified: unauthenticated → `401`; Patient role → `403`
- [ ] Redis cache populated on first call and invalidated after reschedule
- [ ] Audit record created with `ScheduleOverride` event type, old time, new time, and reason

---

## Implementation Checklist

- [ ] Create request/response DTOs with validation attributes (`[Required]`, `[MaxLength]`, `[MinLength]`)
- [ ] Implement `ScheduleService.GetDailyScheduleAsync` with EF Core date-filtered query and Redis caching (30s TTL)
- [ ] Implement `ScheduleService.RescheduleAsync` with conflict detection, transactional time update, and audit logging
- [ ] Create `ScheduleController` with `[Authorize(Roles = "Staff,Admin")]`; validate `DateOnly date` param
- [ ] Modify `AppointmentService` to expose `RescheduleAsync` that updates `DateTime` with conflict validation
- [ ] Invalidate Redis cache keys (`schedule:daily:{date}`, `queue:today:*`) after successful reschedule

---
task_id: task_002
user_story: us_034
epic: EP-004
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_034] Scheduling Override with Mandatory Audit
- **Story Location**: [.propel/context/tasks/EP-004/us_034/us_034.md](.propel/context/tasks/EP-004/us_034/us_034.md)
- **Acceptance Criteria**:
  - AC-1: When a scheduling constraint blocks an action, the API enforces the constraint and returns a structured error indicating override is available for privileged roles.
  - AC-2: When the staff member provides a reason and confirms the override, the scheduling action is completed and an immutable audit record is created capturing identity, constraint, reason, and timestamp.
  - AC-3: When the override request is submitted without a reason, the API validates and returns `400 Bad Request` with "Override reason is required."
  - AC-4: Admins can filter audit records by action type "Override" and retrieve all override events with full reason and actor details.
- **Edge Cases**:
  - Edge Case 1: Override reason exceeds 500 characters; API validates and returns `400` with length error.
  - Edge Case 2: Patient role calls override endpoint; API returns `403 Forbidden`.

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

Implement the scheduling override API endpoint and audit logging service in the `Scheduling` module of the ASP.NET Core 8 Web API. This task exposes `POST /api/v1/scheduling/override` which accepts an override request containing the appointment ID, the scheduling constraint being overridden, the staff-provided reason, and the desired action (cancel, reschedule). The endpoint validates the reason field (required, max 500 chars), verifies the caller has Staff or Admin role, executes the scheduling action bypassing the constraint, and writes an immutable audit record to the `audit_records` table via `IAuditService`. The audit record captures the staff member's identity, the overridden constraint type, the reason, the affected appointment, and the timestamp. Additionally, extend `GET /api/v1/audit` to support an `actionType=Override` filter parameter so admins can retrieve all scheduling override events. All operations run within a single database transaction to ensure the scheduling action and audit write are atomic per DR-002.

---

## Dependent Tasks

- **us_034/task_003** — `scheduling_override_type` column and `override_reason` column on `audit_records` must exist via migration before override audit records can be persisted.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `SchedulingOverrideController` | CREATE | New controller: `POST /api/v1/scheduling/override` |
| `ISchedulingOverrideService` | CREATE | Interface for override execution and constraint bypass |
| `SchedulingOverrideService` | CREATE | Business logic: validate constraint, execute override action, create audit record |
| `OverrideRequest` DTO | CREATE | Request: `AppointmentId`, `ConstraintType`, `Reason`, `Action` |
| `OverrideResponse` DTO | CREATE | Response: `OverrideId`, `AuditRecordId`, `Status`, `AppointmentId` |
| `SchedulingConstraintType` enum | CREATE | `CancellationWithin24Hours`, `RescheduleWithin24Hours`, `SlotConflict`, `CapacityExceeded` |
| `OverrideAction` enum | CREATE | `Cancel`, `Reschedule`, `ForceBook` |
| `IAuditService` | MODIFY | Add method `LogOverrideAsync(OverrideAuditPayload)` for structured override audit entries |
| `AuditController` | MODIFY | Extend `GET /api/v1/audit` with `actionType` query parameter filter |
| `AuditService` | MODIFY | Add override-specific query filter and projection |
| `AppointmentService` | MODIFY | Extract constraint-check logic into reusable method; add `ExecuteWithOverrideAsync` method that bypasses constraint checks |

---

## Implementation Plan

1. **Create `SchedulingConstraintType` enum** in `Scheduling/Domain/Enums/SchedulingConstraintType.cs`: `CancellationWithin24Hours = 0`, `RescheduleWithin24Hours = 1`, `SlotConflict = 2`, `CapacityExceeded = 3`.
2. **Create `OverrideAction` enum** in `Scheduling/Domain/Enums/OverrideAction.cs`: `Cancel = 0`, `Reschedule = 1`, `ForceBook = 2`.
3. **Create DTOs** in `Scheduling/DTOs/`: `OverrideRequest` with `[Required] Guid AppointmentId`, `[Required] SchedulingConstraintType ConstraintType`, `[Required, MaxLength(500), MinLength(1)] string Reason`, `[Required] OverrideAction Action`. `OverrideResponse` with `Guid OverrideId`, `Guid AuditRecordId`, `string Status`, `Guid AppointmentId`.
4. **Create `ISchedulingOverrideService`** interface with `ExecuteOverrideAsync(OverrideRequest request, Guid staffUserId, CancellationToken ct)` returning `Task<OverrideResponse>`.
5. **Implement `SchedulingOverrideService`**:
   - Validate that the appointment exists (return `404` if not).
   - Validate that the constraint type matches the actual violation on the appointment (return `400` "Constraint does not apply" if mismatch — prevent fabricated overrides).
   - Execute the scheduling action via `AppointmentService.ExecuteWithOverrideAsync` which bypasses the normal constraint checks.
   - Create an audit record via `IAuditService.LogOverrideAsync` with: `EventType = Override`, `UserId = staffUserId`, `EntityType = "Appointment"`, `EntityId = appointmentId`, `Details = { ConstraintType, Reason, Action, Timestamp }`.
   - Wrap both operations in a single `IDbContextTransaction` to ensure atomicity per DR-002.
   - Return `OverrideResponse` with the generated IDs.
6. **Create `SchedulingOverrideController`** at route `api/v1/scheduling/override`: `POST /` applies `[Authorize(Roles = "Staff,Admin")]`, delegates to `ISchedulingOverrideService.ExecuteOverrideAsync`, extracts `staffUserId` from `HttpContext.User` claims, returns `Ok(result)`.
7. **Modify `IAuditService` and `AuditService`**: Add `LogOverrideAsync(OverrideAuditPayload payload)` method that writes to `audit_records` with `event_type = 'Override'` and JSONB details containing constraint, reason, and action. Ensure the write is append-only per NFR-010.
8. **Modify `AuditController`**: Extend `GET /api/v1/audit` to accept optional `[FromQuery] string? actionType`. When `actionType = "Override"`, filter `audit_records` by `event_type = 'Override'`. Apply `[Authorize(Roles = "Admin")]` to audit endpoints.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Controllers/
│   │   │   ├── QueueController.cs                     ← EXISTS (us_031)
│   │   │   ├── WalkinController.cs                    ← EXISTS (us_033)
│   │   │   └── SchedulingOverrideController.cs        ← CREATE
│   │   ├── Services/
│   │   │   ├── ISchedulingOverrideService.cs          ← CREATE
│   │   │   ├── SchedulingOverrideService.cs           ← CREATE
│   │   │   └── [existing services...]
│   │   ├── Domain/
│   │   │   └── Enums/
│   │   │       ├── SchedulingConstraintType.cs        ← CREATE
│   │   │       └── OverrideAction.cs                  ← CREATE
│   │   └── DTOs/
│   │       ├── OverrideRequest.cs                     ← CREATE
│   │       └── OverrideResponse.cs                    ← CREATE
│   ├── SharedServices/
│   │   ├── Audit/
│   │   │   ├── IAuditService.cs                       ← MODIFY
│   │   │   ├── AuditService.cs                        ← MODIFY
│   │   │   └── AuditController.cs                     ← MODIFY
│   │   └── [existing shared services...]
│   └── [existing modules...]
├── Program.cs                                          ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after us_031 and us_033 tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Controllers/SchedulingOverrideController.cs` | `POST /api/v1/scheduling/override` with `[Authorize(Roles="Staff,Admin")]` |
| CREATE | `Server/Modules/Scheduling/Services/ISchedulingOverrideService.cs` | Interface for override execution |
| CREATE | `Server/Modules/Scheduling/Services/SchedulingOverrideService.cs` | Business logic: constraint validation, action execution, transactional audit write |
| CREATE | `Server/Modules/Scheduling/Domain/Enums/SchedulingConstraintType.cs` | Enum for constraint types |
| CREATE | `Server/Modules/Scheduling/Domain/Enums/OverrideAction.cs` | Enum for override actions |
| CREATE | `Server/Modules/Scheduling/DTOs/OverrideRequest.cs` | Request DTO with `[Required]`, `[MaxLength(500)]` validation |
| CREATE | `Server/Modules/Scheduling/DTOs/OverrideResponse.cs` | Response DTO with override and audit record IDs |
| MODIFY | `Server/Modules/SharedServices/Audit/IAuditService.cs` | Add `LogOverrideAsync` method signature |
| MODIFY | `Server/Modules/SharedServices/Audit/AuditService.cs` | Implement `LogOverrideAsync` with append-only write |
| MODIFY | `Server/Modules/SharedServices/Audit/AuditController.cs` | Add `actionType` query filter to `GET /api/v1/audit` |
| MODIFY | `Server/Modules/Scheduling/Services/AppointmentService.cs` | Extract constraint-check logic; add `ExecuteWithOverrideAsync` method |
| MODIFY | `Server/Program.cs` | Register `ISchedulingOverrideService` as scoped |

---

## External References

- ASP.NET Core 8 role-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- ASP.NET Core 8 model validation: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#automatic-http-400-responses
- EF Core 8 transactions: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- EF Core 8 JSONB with Npgsql: https://www.npgsql.org/efcore/mapping/json.html
- OpenTelemetry .NET Activity API: https://opentelemetry.io/docs/languages/net/instrumentation/
- FR-SO-004: Staff override of scheduling constraints with mandatory reason capture and audit entry
- NFR-010: Immutable audit evidence for access events, overrides with 7-year retention
- DR-002: Referential integrity and transactional consistency

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

- [ ] Unit tests pass for `SchedulingOverrideService` (mock EF context, mock `IAuditService`, mock `AppointmentService`)
- [ ] Unit tests pass for `AuditService.LogOverrideAsync` (mock EF context)
- [ ] Integration tests pass for `POST /api/v1/scheduling/override` returning `200` with audit record ID
- [ ] Integration tests pass for `POST /api/v1/scheduling/override` returning `400` when reason is empty
- [ ] Integration tests pass for `POST /api/v1/scheduling/override` returning `403` for Patient role
- [ ] Integration tests pass for `GET /api/v1/audit?actionType=Override` returning filtered results
- [ ] Transactional integrity verified: failed audit write rolls back the scheduling action
- [ ] Audit record contains correct staff identity, constraint type, reason, and timestamp

---

## Implementation Checklist

- [ ] Create `SchedulingConstraintType` and `OverrideAction` enums
- [ ] Create `OverrideRequest` DTO with `[Required]`, `[MaxLength(500)]`, `[MinLength(1)]` validation on reason
- [ ] Implement `SchedulingOverrideService.ExecuteOverrideAsync` with constraint validation, action execution, and transactional audit write
- [ ] Create `SchedulingOverrideController` with `[Authorize(Roles = "Staff,Admin")]`; return `403` for unauthorized roles
- [ ] Extend `IAuditService` with `LogOverrideAsync` for structured override audit entries (append-only per NFR-010)
- [ ] Extend `GET /api/v1/audit` with `actionType` query parameter filter; apply `[Authorize(Roles = "Admin")]`
- [ ] Modify `AppointmentService` to expose `ExecuteWithOverrideAsync` that bypasses scheduling constraints
- [ ] Wrap scheduling action and audit write in a single `IDbContextTransaction` for atomicity (DR-002)

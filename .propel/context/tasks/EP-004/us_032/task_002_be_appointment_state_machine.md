---
task_id: task_002
user_story: us_032
epic: EP-004
layer: Backend
status: not-started
effort_hours: 5
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_032] Staff Arrival Check-In Workflow
- **Story Location**: [.propel/context/tasks/EP-004/us_032/us_032.md](.propel/context/tasks/EP-004/us_032/us_032.md)
- **Acceptance Criteria**:
  - AC-1: Check-in records `ArrivedAt` timestamp and transitions `QueueState` to `Arrived`.
  - AC-2: Start Visit records `VisitStartedAt` timestamp and transitions `QueueState` to `InProgress`.
  - AC-3: Complete Visit records `VisitEndedAt` timestamp and transitions `QueueState` to `Completed`.
  - AC-4: No-Show transitions `QueueState` to `NoShow`; audit record written with acting staff member ID (NFR-010).
- **Edge Cases**:
  - Edge Case 1: Invalid transition (e.g., `Completed → InProgress`) must be rejected with a descriptive `InvalidStateTransitionException`; no DB write occurs.

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
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
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

Implement `IAppointmentStateMachineService` and `AppointmentStateMachineService` in the `Scheduling` module. The service owns the state transition rules for the appointment check-in workflow (Scheduled → Arrived → InProgress → Completed / NoShow) and is the sole component permitted to mutate `QueueState` and the corresponding timestamp columns. Each valid transition writes an `AUDIT_RECORD` row (using the existing shared `IAuditService`) with `EventType = "AppointmentStateTransition"`, `EntityType = "Appointment"`, and a `details` JSONB payload containing `fromState`, `toState`, `staffUserId`, and `transitionedAt`. Invalid transitions throw `InvalidStateTransitionException` — no DB write occurs. The service uses an EF Core `DbContext` and executes each transition within a database transaction.

---

## Dependent Tasks

- **us_031 task_004** — `QueueState`, `ArrivedAt`, `VisitStartedAt`, `VisitEndedAt` columns must exist on `Appointments`.
- **EP-DATA/EP-TECH foundational tasks** — `AUDIT_RECORD` table and `IAuditService` must be in place (defined in models.md core data model).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `IAppointmentStateMachineService` | CREATE | Interface for DI and testability |
| `AppointmentStateMachineService` | CREATE | State transition logic + audit write |
| `InvalidStateTransitionException` | CREATE | Domain exception for illegal state transitions |
| `AppointmentStateAction` (enum) | CREATE | `CheckIn = 0, StartVisit = 1, CompleteVisit = 2, NoShow = 3` |
| `SchedulingModule` DI registration | MODIFY | Register `IAppointmentStateMachineService` as scoped |

---

## Implementation Plan

1. **Define `AppointmentStateAction` enum** in `Scheduling/Domain/Enums/AppointmentStateAction.cs`: `CheckIn = 0, StartVisit = 1, CompleteVisit = 2, NoShow = 3`.
2. **Define `InvalidStateTransitionException`** in `Scheduling/Domain/Exceptions/InvalidStateTransitionException.cs`: inherits from `DomainException` (or `Exception`); message format: `"Cannot perform '{action}' on appointment in state '{currentState}'."`.
3. **Create `IAppointmentStateMachineService`** with method `TransitionAsync(Guid appointmentId, AppointmentStateAction action, Guid staffUserId, CancellationToken ct): Task<Appointment>`.
4. **Implement valid transitions map** as a static `IReadOnlyDictionary<AppointmentStateAction, (QueueState From, QueueState To)>` constant:
   - `CheckIn → (Scheduled, Arrived)`
   - `StartVisit → (Arrived, InProgress)`
   - `CompleteVisit → (InProgress, Completed)`
   - `NoShow → (Scheduled | Arrived, NoShow)` — validate `From` is one of `{Scheduled, Arrived}`
5. **Implement `TransitionAsync`**:
   - Load `Appointment` by ID; throw `NotFoundException` if not found.
   - Look up expected `(From, To)` pair; validate `appointment.QueueState == From`; throw `InvalidStateTransitionException` if mismatch.
   - Set `appointment.QueueState = To`; set the corresponding timestamp (`ArrivedAt`, `VisitStartedAt`, `VisitEndedAt`) to `DateTimeOffset.UtcNow`.
   - Call `IAuditService.RecordAsync(EventType.AppointmentStateTransition, "Appointment", appointmentId, details, staffUserId, ct)`.
   - `await dbContext.SaveChangesAsync(ct)` — both `Appointment` update and `AUDIT_RECORD` insert within same EF Core transaction.
6. **Register** `IAppointmentStateMachineService → AppointmentStateMachineService` as scoped in `SchedulingModule` DI setup.
7. **Add OpenTelemetry span** `appointment.state.transition` with tags `appointment.id`, `action`, `from_state`, `to_state` for observability (NFR-011).

---

## Current Project State

```
Server/
├── Modules/
│   └── Scheduling/
│       ├── Domain/
│       │   ├── Enums/
│       │   │   ├── QueueState.cs                        ← EXISTS (us_031 task_004)
│       │   │   └── AppointmentStateAction.cs            ← CREATE
│       │   └── Exceptions/
│       │       └── InvalidStateTransitionException.cs   ← CREATE
│       └── Services/
│           ├── IAppointmentStateMachineService.cs       ← CREATE
│           └── AppointmentStateMachineService.cs        ← CREATE
└── Shared/
    └── Services/
        └── IAuditService.cs                             ← EXISTS (EP-DATA/EP-TECH)
```

> Placeholder: Update once EP-DATA/EP-TECH task artifacts confirm `IAuditService` path and the `SchedulingModule` registration file location.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Domain/Enums/AppointmentStateAction.cs` | Enum: `CheckIn, StartVisit, CompleteVisit, NoShow` |
| CREATE | `Server/Modules/Scheduling/Domain/Exceptions/InvalidStateTransitionException.cs` | Domain exception with descriptive message |
| CREATE | `Server/Modules/Scheduling/Services/IAppointmentStateMachineService.cs` | Interface: `TransitionAsync(appointmentId, action, staffUserId, ct)` |
| CREATE | `Server/Modules/Scheduling/Services/AppointmentStateMachineService.cs` | Transition map, state validation, EF Core update, audit write, OTel span |
| MODIFY | `Server/Modules/Scheduling/SchedulingModule.cs` (or `Program.cs`) | Register `IAppointmentStateMachineService → AppointmentStateMachineService` as scoped |

---

## External References

- EF Core 8 transactions (implicit via `SaveChangesAsync`): https://learn.microsoft.com/en-us/ef/core/saving/transactions
- ASP.NET Core 8 domain exceptions pattern: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
- OpenTelemetry .NET Activity API (`Activity.Current?.SetTag`): https://opentelemetry.io/docs/languages/net/instrumentation/
- NFR-010 — Audit evidence: 7-year immutable retention; `AUDIT_RECORD` is append-only (no UPDATE/DELETE permitted on that table)
- State machine pattern (C# dictionary-based): https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching

---

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test --filter "Category=StateMachine"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass: `CheckIn` from `Scheduled` succeeds; `ArrivedAt` is set; `AUDIT_RECORD` insert called once
- [ ] Unit tests pass: `StartVisit` from `Scheduled` (invalid) throws `InvalidStateTransitionException`; no DB write
- [ ] Unit tests pass: `NoShow` from `Arrived` succeeds; `NoShow` from `Completed` throws exception
- [ ] Unit tests pass: `NotFoundException` thrown for non-existent `appointmentId`
- [ ] `AUDIT_RECORD` row contains correct `staffUserId`, `fromState`, `toState`, `transitionedAt` in JSONB `details`
- [ ] Both `Appointment` update and `AUDIT_RECORD` insert committed atomically (roll back together on error)
- [ ] OpenTelemetry span `appointment.state.transition` appears in trace output with correct tags

---

## Implementation Checklist

- [ ] Create `AppointmentStateAction` enum (`CheckIn`, `StartVisit`, `CompleteVisit`, `NoShow`)
- [ ] Create `InvalidStateTransitionException` with message template `"Cannot perform '{action}' on appointment in state '{currentState}'"`
- [ ] Create `IAppointmentStateMachineService` interface with `TransitionAsync` signature
- [ ] Implement static valid transitions map as `IReadOnlyDictionary<AppointmentStateAction, (QueueState, QueueState)>`
- [ ] Implement `TransitionAsync`: load appointment, validate state, apply transition + timestamp, call `IAuditService.RecordAsync`, save changes
- [ ] Add OpenTelemetry span `appointment.state.transition` with `appointment.id`, `action`, `from_state`, `to_state` tags (NFR-011)
- [ ] Register `IAppointmentStateMachineService` as scoped in `SchedulingModule`

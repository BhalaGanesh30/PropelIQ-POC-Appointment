---
task_id: task_003
user_story: us_031
epic: EP-004
layer: Backend
status: not-started
effort_hours: 4
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_031] Real-Time Queue Dashboard
- **Story Location**: [.propel/context/tasks/EP-004/us_031/us_031.md](.propel/context/tasks/EP-004/us_031/us_031.md)
- **Acceptance Criteria**:
  - AC-1: Wait-time estimates are displayed for each queue entry when the dashboard loads within 3 seconds (service must compute estimates fast enough to not block AC-1).
  - AC-3: Overdue detection logic must correctly flag patients who have been waiting longer than their `estimatedWaitMinutes`.
- **Edge Cases**:
  - Edge Case 2: Queue with 100+ patients — wait-time algorithm must be O(n) or better; no nested loops over the patient list.

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

Implement `IWaitTimeEstimationService` and `WaitTimeEstimationService` in the `Scheduling` module. The service computes an `EstimatedWaitMinutes` value for each queue entry based on that patient's queue position and the configurable average service duration for the appointment type. It also exposes an `IsOverdue` check comparing elapsed wait against the estimate. The algorithm uses a single ordered O(n) pass over the queue to assign queue positions, avoiding nested loops. The service is pure and side-effect-free — no I/O beyond reading appointment type durations — making it directly unit-testable. It is consumed by `QueueService` (task_002).

---

## Dependent Tasks

- **task_004** — `Appointment.QueueState` and `Appointment.ArrivedAt` columns must exist (applied via DB migration) for `QueueService` to pass meaningful data into this service.

> This service itself is pure (no EF Core / DB dependency) and can be implemented and unit-tested independently of task_004.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `IWaitTimeEstimationService` | CREATE | Interface for DI — consumed by `QueueService` |
| `WaitTimeEstimationService` | CREATE | Pure implementation with O(n) algorithm |
| `WaitTimeOptions` | CREATE | `IOptions<WaitTimeOptions>` config class (`DefaultServiceDurationMinutes`, `AppointmentTypeDurations` dictionary) |
| `SchedulingModule` DI registration | MODIFY | Register `IWaitTimeEstimationService → WaitTimeEstimationService` as singleton |
| `appsettings.json` | MODIFY | Add `WaitTime` config section with default duration and per-type overrides |

---

## Implementation Plan

1. **Define `WaitTimeOptions` configuration class** in `Scheduling/Configuration/WaitTimeOptions.cs`:
   - `int DefaultServiceDurationMinutes = 15` (fallback when appointment type not found in dictionary)
   - `Dictionary<string, int> AppointmentTypeDurations` (keyed by appointment type code, e.g., `"GENERAL": 20, "FOLLOWUP": 10`)
   - Bind from `appsettings.json` section `"WaitTime"` using `services.Configure<WaitTimeOptions>(config.GetSection("WaitTime"))`.
2. **Create `IWaitTimeEstimationService`** in `Scheduling/Services/IWaitTimeEstimationService.cs`:
   - Method: `int CalculateEstimatedWaitMinutes(int queuePosition, string appointmentTypeCode)`
   - Method: `bool IsOverdue(DateTimeOffset? arrivedAt, int estimatedWaitMinutes)`
3. **Implement `WaitTimeEstimationService`**:
   - `CalculateEstimatedWaitMinutes`: Lookup duration from `WaitTimeOptions.AppointmentTypeDurations` dictionary (fallback to `DefaultServiceDurationMinutes`); return `queuePosition * durationMinutes`.
   - `IsOverdue`: Return `arrivedAt.HasValue && DateTimeOffset.UtcNow - arrivedAt.Value > TimeSpan.FromMinutes(estimatedWaitMinutes)`.
4. **O(n) queue position contract**: Document in XML comment that callers (i.e., `QueueService`) are responsible for computing queue positions via a single LINQ `Select` with an index (`queue.Select((entry, index) => ...)`) before invoking `CalculateEstimatedWaitMinutes`; this ensures the overall algorithm remains O(n).
5. **Register in DI**: `services.AddSingleton<IWaitTimeEstimationService, WaitTimeEstimationService>()` — singleton is safe because the service holds only read-only configuration state.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Configuration/
│   │   │   └── WaitTimeOptions.cs              ← CREATE
│   │   ├── Services/
│   │   │   ├── IWaitTimeEstimationService.cs   ← CREATE
│   │   │   └── WaitTimeEstimationService.cs    ← CREATE
│   │   └── [existing Scheduling files...]
└── appsettings.json                             ← MODIFY
```

> Placeholder: Update tree once task_002 and task_004 file paths are confirmed during execution.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Configuration/WaitTimeOptions.cs` | Config POCO: `DefaultServiceDurationMinutes`, `AppointmentTypeDurations` dictionary |
| CREATE | `Server/Modules/Scheduling/Services/IWaitTimeEstimationService.cs` | Service interface with `CalculateEstimatedWaitMinutes` and `IsOverdue` methods |
| CREATE | `Server/Modules/Scheduling/Services/WaitTimeEstimationService.cs` | Pure implementation with config lookup; no DB I/O |
| MODIFY | `Server/Program.cs` | `services.Configure<WaitTimeOptions>(...)`; register `IWaitTimeEstimationService` as singleton |
| MODIFY | `Server/appsettings.json` | Add `"WaitTime": { "DefaultServiceDurationMinutes": 15, "AppointmentTypeDurations": { "GENERAL": 20, "FOLLOWUP": 10, "URGENT": 30 } }` |

---

## External References

- ASP.NET Core 8 `IOptions<T>` configuration pattern: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-8.0
- C# `Dictionary<TKey, TValue>` with `TryGetValue` (O(1) lookup for per-type durations): https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2
- LINQ `Select` with index (O(n) single-pass queue position): https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.select

---

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run unit tests for WaitTimeEstimationService
dotnet test --filter "Category=WaitTime"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass: `CalculateEstimatedWaitMinutes(0, "GENERAL")` returns `0`; `CalculateEstimatedWaitMinutes(3, "GENERAL")` returns `60` (3 × 20)
- [ ] Unit tests pass: `IsOverdue(DateTimeOffset.UtcNow.AddMinutes(-25), 20)` returns `true`
- [ ] Unit tests pass: `IsOverdue(null, 20)` returns `false` (patient not yet arrived)
- [ ] Unit tests pass: unknown appointment type falls back to `DefaultServiceDurationMinutes`
- [ ] Service registered as singleton (no per-request object allocation)
- [ ] No EF Core or database calls inside `WaitTimeEstimationService` (pure computation verified by code review)
- [ ] XML doc comment on `CalculateEstimatedWaitMinutes` documents the O(n) contract with the caller

---

## Implementation Checklist

- [ ] Create `WaitTimeOptions` config POCO in `Scheduling/Configuration/WaitTimeOptions.cs`
- [ ] Create `IWaitTimeEstimationService` interface with `CalculateEstimatedWaitMinutes` and `IsOverdue` method signatures
- [ ] Implement `WaitTimeEstimationService.CalculateEstimatedWaitMinutes` with dictionary lookup and `DefaultServiceDurationMinutes` fallback
- [ ] Implement `WaitTimeEstimationService.IsOverdue` returning `true` when elapsed wait exceeds estimate
- [ ] Add XML doc comment on `CalculateEstimatedWaitMinutes` documenting that callers must compute queue position via O(n) single `Select` pass
- [ ] Register `IWaitTimeEstimationService → WaitTimeEstimationService` as singleton in `Program.cs`
- [ ] Bind `WaitTimeOptions` from `appsettings.json` section `"WaitTime"` with sensible defaults

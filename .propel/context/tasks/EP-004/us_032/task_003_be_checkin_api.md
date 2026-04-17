---
task_id: task_003
user_story: us_032
epic: EP-004
layer: Backend
status: not-started
effort_hours: 4
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_032] Staff Arrival Check-In Workflow
- **Story Location**: [.propel/context/tasks/EP-004/us_032/us_032.md](.propel/context/tasks/EP-004/us_032/us_032.md)
- **Acceptance Criteria**:
  - AC-1: `PATCH /api/v1/appointments/{id}/state` with `action: "check-in"` transitions appointment to `Arrived` and returns updated entry.
  - AC-2: `action: "start-visit"` transitions to `InProgress`; `action: "complete-visit"` transitions to `Completed`.
  - AC-4: `action: "no-show"` transitions to `NoShow`; endpoint is Staff/Admin only (FR-SO-002).
- **Edge Cases**:
  - Edge Case 1: Invalid `action` value → `HTTP 400`; invalid state transition → `HTTP 422 Unprocessable Entity` with server error message.

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

Implement `AppointmentStateController` in the `Scheduling` module exposing `PATCH /api/v1/appointments/{id}/state`. The controller accepts a `TransitionStateRequest` DTO (`action` string enum), resolves the authenticated staff user's ID from the JWT claims, and delegates to `IAppointmentStateMachineService.TransitionAsync`. It maps domain exceptions to HTTP status codes: `NotFoundException → 404`, `InvalidStateTransitionException → 422`, invalid `action` value → `400` (via `[ApiController]` model binding). On success it returns `HTTP 200` with a `QueueEntryDto` reflecting the updated state. The endpoint is protected with `[Authorize(Roles = "Staff,Admin")]` (FR-SO-002).

---

## Dependent Tasks

- **task_002** (us_032) — `IAppointmentStateMachineService` must be implemented before this controller can be wired up and tested end-to-end.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `AppointmentStateController` | CREATE | `PATCH /api/v1/appointments/{id}/state` endpoint |
| `TransitionStateRequest` | CREATE | Request DTO: `Action` string validated against `AppointmentStateAction` enum |
| `AppointmentStateErrorHandler` (middleware / exception filter) | MODIFY or CREATE | Map `InvalidStateTransitionException → 422`; `NotFoundException → 404` |
| `SchedulingModule` routing | MODIFY | Ensure controller is discovered in the Scheduling module |

---

## Implementation Plan

1. **Create `TransitionStateRequest` DTO** in `Scheduling/DTOs/TransitionStateRequest.cs`: property `public AppointmentStateAction Action { get; set; }` — ASP.NET Core's `[ApiController]` will return `HTTP 400` automatically if an invalid enum string is provided.
2. **Create `AppointmentStateController`** decorated with `[ApiController]`, `[Route("api/v1/appointments")]`, and `[Authorize(Roles = "Staff,Admin")]`.
3. **Implement `PATCH {id}/state` action**: bind `[FromRoute] Guid id` and `[FromBody] TransitionStateRequest request`; extract `staffUserId` from `User.FindFirstValue(ClaimTypes.NameIdentifier)`; call `await _stateMachineService.TransitionAsync(id, request.Action, staffUserId, ct)`.
4. **Map domain exceptions to HTTP responses**: use an `IExceptionHandler` (ASP.NET Core 8) or `try/catch`:
   - `NotFoundException` → `return NotFound(new { message = ex.Message })`
   - `InvalidStateTransitionException` → `return UnprocessableEntity(new { message = ex.Message })`
5. **Project result to `QueueEntryDto`**: reuse the DTO from us_031 task_002; map updated `Appointment` fields (status, timestamps, `isOverdue` from `IWaitTimeEstimationService`).
6. **Return `OK(dto)`** on success with `HTTP 200`.
7. **Write XML doc comment** on the action documenting `HTTP 200`, `400`, `401`, `403`, `404`, and `422` response codes for OpenAPI generation.
8. **Register controller** in the Scheduling module — ensure it is included in `AddControllers()` assembly scan or explicit registration.

---

## Current Project State

```
Server/
├── Modules/
│   └── Scheduling/
│       ├── Controllers/
│       │   ├── QueueController.cs               ← EXISTS (us_031 task_002)
│       │   └── AppointmentStateController.cs    ← CREATE
│       └── DTOs/
│           ├── QueueEntryDto.cs                 ← EXISTS (us_031 task_002)
│           └── TransitionStateRequest.cs        ← CREATE
├── Shared/
│   └── Exceptions/
│       └── NotFoundException.cs                 ← EXISTS or CREATE if absent
└── Program.cs                                   ← MODIFY if exception handler not yet registered
```

> Placeholder: Update tree once task_002 artifacts are confirmed and the exception handling strategy (global filter vs. `IExceptionHandler`) is agreed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Controllers/AppointmentStateController.cs` | `PATCH /api/v1/appointments/{id}/state` with auth, delegation, exception mapping |
| CREATE | `Server/Modules/Scheduling/DTOs/TransitionStateRequest.cs` | Request DTO with `AppointmentStateAction Action` property |
| MODIFY | `Server/Program.cs` (or global exception handler) | Register `InvalidStateTransitionException → 422` mapping if not handled via controller `try/catch` |

---

## External References

- ASP.NET Core 8 `IExceptionHandler` (preferred over middleware for typed exceptions): https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0#iexceptionhandler
- ASP.NET Core 8 JWT claims extraction (`User.FindFirstValue`): https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-8.0
- ASP.NET Core 8 `[ApiController]` automatic 400 for invalid enum binding: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#automatic-http-400-responses
- HTTP 422 Unprocessable Entity usage for business rule violations: https://www.rfc-editor.org/rfc/rfc9110#name-422-unprocessable-content
- FR-SO-002: Staff-only check-in — `[Authorize(Roles = "Staff,Admin")]` enforces this requirement

---

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test --filter "Category=CheckinApi"

# Run API locally (requires task_002 and task_004 from us_031 to be applied)
dotnet run --project Server/Server.csproj
```

---

## Implementation Validation Strategy

- [ ] `PATCH /api/v1/appointments/{id}/state` with `{"action":"check-in"}` returns `HTTP 200` with updated `QueueEntryDto` for an authenticated Staff user
- [ ] Same request from unauthenticated caller returns `HTTP 401`
- [ ] Same request from Patient role returns `HTTP 403`
- [ ] `{"action":"invalid_value"}` returns `HTTP 400` (model binding)
- [ ] Valid action on wrong state (e.g., `complete-visit` on `Scheduled`) returns `HTTP 422` with descriptive `message` field
- [ ] Non-existent appointment ID returns `HTTP 404`
- [ ] `staffUserId` in returned audit record matches JWT `sub` claim of the requesting user
- [ ] Integration test confirms `AUDIT_RECORD` row inserted after successful transition

---

## Implementation Checklist

- [ ] Create `TransitionStateRequest` DTO with `AppointmentStateAction Action` property
- [ ] Create `AppointmentStateController` with `[ApiController]`, `[Route("api/v1/appointments")]`, `[Authorize(Roles="Staff,Admin")]`
- [ ] Implement `PATCH {id}/state` action: extract `staffUserId` from JWT claims, delegate to `IAppointmentStateMachineService`
- [ ] Map `NotFoundException → HTTP 404` and `InvalidStateTransitionException → HTTP 422` with `{ message }` response body
- [ ] Project updated `Appointment` to `QueueEntryDto` using `IWaitTimeEstimationService` for `estimatedWaitMinutes`
- [ ] Add XML doc comments for all HTTP response codes (200, 400, 401, 403, 404, 422) for OpenAPI
- [ ] Register global `InvalidStateTransitionException` handler if not already present in `Program.cs`
- [ ] Verify controller is included in `AddControllers()` assembly scan (no manual registration needed if in same assembly)

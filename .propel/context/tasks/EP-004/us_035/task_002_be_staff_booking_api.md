---
task_id: task_002
user_story: us_035
epic: EP-004
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_035] Staff-Assisted Patient Booking
- **Story Location**: [.propel/context/tasks/EP-004/us_035/us_035.md](.propel/context/tasks/EP-004/us_035/us_035.md)
- **Acceptance Criteria**:
  - AC-1: Staff searches for a patient and selects a slot; booking is created without patient-side verification requirements.
  - AC-2: Patient receives standard confirmation email and ICS artifacts; booking is attributed to the staff member who created it.
  - AC-3: Staff can create a basic patient profile inline and attach the booking to the new profile.
  - AC-4: Audit log shows the booking was created by a staff actor on behalf of the patient.
- **Edge Cases**:
  - Edge Case 1: Patient has a conflicting appointment at the same time; API returns conflict details and allows override with acknowledgment.
  - Edge Case 2: Staff cannot book for themselves; API validates that `staffUserId != patientUserId`.

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

Implement the staff-assisted booking API endpoint, conflict check, inline patient creation, and audit attribution in the `Scheduling` module of the ASP.NET Core 8 Web API. This task exposes `POST /api/v1/staff-bookings` which creates an appointment on behalf of a patient without requiring patient-side verification. The endpoint accepts the patient ID (or inline patient creation payload), selected slot ID, visit reason, and optional override reason (if a scheduling conflict exists). The booking is atomically reserved, the `created_by_staff_id` column is populated for audit attribution, and the standard confirmation notification (email + ICS) is triggered via `INotificationService`. Additionally, expose `GET /api/v1/appointments/conflict-check?patientId={id}&slotId={id}` to detect scheduling conflicts before booking submission. A self-booking guard validates that the authenticated staff member is not booking for their own patient profile. All operations are secured with `[Authorize(Roles = "Staff,Admin")]`.

---

## Dependent Tasks

- **us_035/task_003** — `created_by_staff_id` column on `appointments` table must exist via migration.
- **us_033/task_002** — `PatientSearchController` (`GET /api/v1/patients/search`) is reused for patient lookup.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `StaffBookingController` | CREATE | New controller: `POST /api/v1/staff-bookings`, `GET /api/v1/appointments/conflict-check` |
| `IStaffBookingService` | CREATE | Interface for staff-assisted booking creation |
| `StaffBookingService` | CREATE | Business logic: conflict check, inline patient creation, slot reservation, audit attribution, notification trigger |
| `CreateStaffBookingRequest` DTO | CREATE | Request: `PatientId?`, `SlotId`, `VisitReason`, `OverrideReason?`, `NewPatient?` (inline creation) |
| `StaffBookingResponse` DTO | CREATE | Response: `BookingId`, `AppointmentId`, `ConfirmationUrl`, `StaffActorId`, `PatientId` |
| `InlinePatientPayload` DTO | CREATE | Nested DTO: `FirstName`, `LastName`, `Phone`, `DateOfBirth`, `Email?` |
| `ConflictCheckResponse` DTO | CREATE | Response: `HasConflict`, `ConflictingAppointmentId?`, `ConflictingDateTime?`, `ConflictingReason?` |
| `AppointmentService` | MODIFY | Add `ReserveSlotForStaffAsync` method bypassing patient verification; populate `created_by_staff_id` |
| `IAuditService` | MODIFY | Add `LogStaffBookingAsync` for staff-on-behalf-of audit entries |
| `INotificationService` | REUSE | Trigger standard confirmation (email + PDF + ICS) for the patient |
| `Appointment` entity | MODIFY | Add `CreatedByStaffId` nullable FK property |

---

## Implementation Plan

1. **Create DTOs** in `Scheduling/DTOs/`: `CreateStaffBookingRequest` with `[Required] Guid SlotId`, `Guid? PatientId`, `[Required, MaxLength(500)] string VisitReason`, `[MaxLength(300)] string? OverrideReason`, `InlinePatientPayload? NewPatient`. `InlinePatientPayload` with `[Required, MaxLength(100)] string FirstName`, `[Required, MaxLength(100)] string LastName`, `[Required] string Phone`, `[Required] DateOnly DateOfBirth`, `string? Email`. `StaffBookingResponse` with booking details. `ConflictCheckResponse` with conflict details.
2. **Create `IStaffBookingService`** interface with `CreateBookingAsync(CreateStaffBookingRequest request, Guid staffUserId, CancellationToken ct)` returning `Task<StaffBookingResponse>` and `CheckConflictAsync(Guid patientId, Guid slotId, CancellationToken ct)` returning `Task<ConflictCheckResponse>`.
3. **Implement `StaffBookingService.CheckConflictAsync`**: Query `Appointments` table for the patient on the same date and overlapping time range as the selected slot. Return `ConflictCheckResponse` with conflict details or `HasConflict = false`.
4. **Implement `StaffBookingService.CreateBookingAsync`**:
   - **Self-booking guard**: Resolve the staff user's linked patient ID (if any). If `request.PatientId == staffPatientId`, return `400 Bad Request` with "Staff-assisted booking cannot be used for self-booking."
   - **Inline patient creation**: If `request.NewPatient` is provided and `request.PatientId` is null, create a new `Patient` record and `User` account (with a pending verification status) via a shared patient creation service. Set `request.PatientId` to the new patient's ID.
   - **Conflict check**: If `request.OverrideReason` is null, call `CheckConflictAsync`. If conflict exists, return `409 Conflict` with conflict details prompting the frontend to collect an override reason.
   - **Slot reservation**: Call `AppointmentService.ReserveSlotForStaffAsync` which atomically reserves the slot, sets `Appointment.PatientId`, `Appointment.CreatedByStaffId = staffUserId`, `Appointment.Reason = VisitReason`. Bypass patient-side verification checks.
   - **Notification**: Trigger `INotificationService.SendBookingConfirmationAsync` with the patient's email, generating PDF summary, QR code, and ICS attachment.
   - **Audit**: Log via `IAuditService.LogStaffBookingAsync` with `EventType = StaffBooking`, `UserId = staffUserId`, `EntityType = Appointment`, `EntityId = appointmentId`, `Details = { PatientId, SlotId, OnBehalfOf: true }`.
   - **Cache invalidation**: Remove Redis cache key `slots:*` and `queue:today:*` after successful booking.
   - Return `StaffBookingResponse`.
5. **Create `StaffBookingController`** at route `api/v1/staff-bookings`: `POST /` applies `[Authorize(Roles = "Staff,Admin")]`, extracts `staffUserId` from claims, delegates to service. Route `GET /api/v1/appointments/conflict-check` applies same authorization, accepts `[FromQuery] Guid patientId` and `[FromQuery] Guid slotId`.
6. **Modify `Appointment` entity**: Add `Guid? CreatedByStaffId` property with FK to `Users` table. This is populated only for staff-assisted bookings; patient self-bookings leave it null.
7. **Modify `IAuditService`**: Add `LogStaffBookingAsync(StaffBookingAuditPayload)` writing `event_type = 'StaffBooking'` with JSONB details including staff identity and on-behalf-of flag.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Controllers/
│   │   │   ├── QueueController.cs                      ← EXISTS (us_031)
│   │   │   ├── WalkinController.cs                     ← EXISTS (us_033)
│   │   │   ├── SchedulingOverrideController.cs         ← EXISTS (us_034)
│   │   │   └── StaffBookingController.cs               ← CREATE
│   │   ├── Services/
│   │   │   ├── IStaffBookingService.cs                 ← CREATE
│   │   │   ├── StaffBookingService.cs                  ← CREATE
│   │   │   ├── AppointmentService.cs                   ← MODIFY
│   │   │   └── [existing services...]
│   │   └── DTOs/
│   │       ├── CreateStaffBookingRequest.cs             ← CREATE
│   │       ├── StaffBookingResponse.cs                  ← CREATE
│   │       ├── InlinePatientPayload.cs                  ← CREATE
│   │       └── ConflictCheckResponse.cs                 ← CREATE
│   ├── SharedServices/
│   │   └── Audit/
│   │       ├── IAuditService.cs                         ← MODIFY
│   │       └── AuditService.cs                          ← MODIFY
│   └── [existing modules...]
├── Program.cs                                            ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Controllers/StaffBookingController.cs` | `POST /api/v1/staff-bookings` and `GET /api/v1/appointments/conflict-check` |
| CREATE | `Server/Modules/Scheduling/Services/IStaffBookingService.cs` | Interface for staff booking creation and conflict check |
| CREATE | `Server/Modules/Scheduling/Services/StaffBookingService.cs` | Business logic: self-booking guard, inline patient creation, slot reservation, audit, notification |
| CREATE | `Server/Modules/Scheduling/DTOs/CreateStaffBookingRequest.cs` | Request DTO with validation attributes |
| CREATE | `Server/Modules/Scheduling/DTOs/StaffBookingResponse.cs` | Response DTO with booking and attribution details |
| CREATE | `Server/Modules/Scheduling/DTOs/InlinePatientPayload.cs` | Nested DTO for inline patient creation |
| CREATE | `Server/Modules/Scheduling/DTOs/ConflictCheckResponse.cs` | Conflict detection response DTO |
| MODIFY | `Server/Modules/Scheduling/Domain/Entities/Appointment.cs` | Add `CreatedByStaffId` nullable FK property |
| MODIFY | `Server/Modules/Scheduling/Services/AppointmentService.cs` | Add `ReserveSlotForStaffAsync` bypassing patient verification |
| MODIFY | `Server/Modules/SharedServices/Audit/IAuditService.cs` | Add `LogStaffBookingAsync` method |
| MODIFY | `Server/Modules/SharedServices/Audit/AuditService.cs` | Implement staff booking audit entry with on-behalf-of details |
| MODIFY | `Server/Program.cs` | Register `IStaffBookingService` as scoped |

---

## External References

- ASP.NET Core 8 Web API controller routing: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0
- EF Core 8 transactions for atomic slot reservation: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- ASP.NET Core 8 role-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- ASP.NET Core 8 model validation: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#automatic-http-400-responses
- FR-SO-005: Staff create bookings on behalf of patients without patient-side verification
- NFR-010: Immutable audit evidence for booking events
- DR-002: Referential integrity and transactional consistency for booking

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

- [ ] Unit tests pass for `StaffBookingService` (mock EF context, mock `INotificationService`, mock `IAuditService`)
- [ ] Unit tests pass for `StaffBookingService.CheckConflictAsync` (mock EF context)
- [ ] Integration tests pass for `POST /api/v1/staff-bookings` returning `201` with booking ID
- [ ] Integration tests pass for `POST /api/v1/staff-bookings` returning `400` on self-booking attempt
- [ ] Integration tests pass for `POST /api/v1/staff-bookings` returning `409` on conflict without override reason
- [ ] Integration tests pass for `GET /api/v1/appointments/conflict-check` returning conflict details
- [ ] Authorization verified: unauthenticated → `401`; Patient role → `403`
- [ ] Audit record created with `StaffBooking` event type and on-behalf-of details
- [ ] Notification triggered: confirmation email with PDF + ICS sent to patient

---

## Implementation Checklist

- [ ] Create request/response DTOs with validation attributes (`[Required]`, `[MaxLength]`)
- [ ] Implement `StaffBookingService.CheckConflictAsync` querying overlapping appointments for the patient
- [ ] Implement `StaffBookingService.CreateBookingAsync` with self-booking guard, inline patient creation, slot reservation, and audit
- [ ] Create `StaffBookingController` with `[Authorize(Roles = "Staff,Admin")]`; extract staff identity from JWT claims
- [ ] Modify `AppointmentService.ReserveSlotForStaffAsync` to bypass patient verification and populate `CreatedByStaffId`
- [ ] Extend `IAuditService` with `LogStaffBookingAsync` for staff-on-behalf-of audit entries
- [ ] Trigger `INotificationService.SendBookingConfirmationAsync` for patient email + ICS after successful booking
- [ ] Invalidate Redis cache keys (`slots:*`, `queue:today:*`) after successful booking

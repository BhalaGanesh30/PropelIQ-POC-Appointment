---
task_id: task_002
user_story: us_033
epic: EP-004
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_033] Walk-In Creation and Patient Registration Conversion
- **Story Location**: [.propel/context/tasks/EP-004/us_033/us_033.md](.propel/context/tasks/EP-004/us_033/us_033.md)
- **Acceptance Criteria**:
  - AC-1: Staff creates a walk-in entry with patient name and visit reason; a temporary walk-in record is created and inserted into the queue with an estimated wait-time position.
  - AC-2: Staff initiates patient registration for a walk-in; a new patient account is created and the walk-in record is associated with the new patient profile.
  - AC-3: Walk-in appears on the queue dashboard with a "Walk-In" label distinguishing it from scheduled patients.
  - AC-4: Existing patient search by name or phone number finds the profile; walk-in created against existing account without duplication.
- **Edge Cases**:
  - Edge Case 1: Multiple patients match search; a disambiguation list is returned with patient demographics (name, DOB, phone).
  - Edge Case 2: Clinic at maximum capacity; walk-in creation is still allowed but the response includes a capacity warning flag.

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

Implement the walk-in creation, patient search, and walk-in-to-patient conversion API endpoints in the `Scheduling` module of the ASP.NET Core 8 Web API. This task exposes three endpoints: `POST /api/v1/walkins` to create a walk-in record and insert it into the queue, `GET /api/v1/patients/search` to search existing patients by name or phone for disambiguation, and `POST /api/v1/walkins/{id}/convert` to convert a temporary walk-in record into a registered patient account. All endpoints are secured with `[Authorize(Roles = "Staff,Admin")]`. The walk-in creation endpoint returns the queue position and a capacity warning flag when the clinic exceeds the configured threshold. The patient search endpoint returns matching patient demographics (name, DOB, phone) for disambiguation when multiple results match. Walk-in records are persisted in the `WalkIns` table (task_003) and linked to the `Appointments` queue via a `QueueState = Waiting` entry with `AppointmentType = WalkIn`.

---

## Dependent Tasks

- **us_033/task_003** — `WalkIns` table migration must be applied so the `WalkIn` entity and `AppointmentType.WalkIn` enum value exist.
- **us_031/task_002** — `QueueController` and `QueueService` must exist; walk-in creation reuses `IQueueService` for queue insertion and position calculation.
- **us_031/task_003** — `IWaitTimeEstimationService` must be available for estimated wait-time calculation on walk-in queue insertion.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `WalkinController` | CREATE | New controller: `POST /api/v1/walkins`, `POST /api/v1/walkins/{id}/convert` |
| `PatientSearchController` | CREATE | New controller: `GET /api/v1/patients/search` |
| `IWalkinService` | CREATE | Interface for walk-in creation and conversion logic |
| `WalkinService` | CREATE | Business logic: create walk-in, queue insertion, capacity check, convert to patient |
| `IPatientSearchService` | CREATE | Interface for patient search by name or phone |
| `PatientSearchService` | CREATE | EF Core query with name/phone filter, returns demographics |
| `CreateWalkinRequest` DTO | CREATE | Request: `PatientName`, `Phone`, `VisitReason`, `ExistingPatientId?`, `ConvertToPatient`, `DateOfBirth?`, `Email?` |
| `WalkinResponse` DTO | CREATE | Response: `WalkinId`, `QueuePosition`, `EstimatedWaitMinutes`, `IsAtCapacity`, `PatientId?` |
| `ConvertWalkinRequest` DTO | CREATE | Request: `DateOfBirth`, `Email`, `Phone` |
| `ConvertWalkinResponse` DTO | CREATE | Response: `PatientId`, `WalkinId`, `ConversionStatus` |
| `PatientSearchResultDto` | CREATE | Response: `PatientId`, `FirstName`, `LastName`, `DateOfBirth`, `Phone` |
| `WalkIn` entity | CREATE | Domain entity mapped to `WalkIns` table |
| `AppointmentType` enum | MODIFY | Add `WalkIn` value |
| `SchedulingModule` DI registration | MODIFY | Register `WalkinService`, `PatientSearchService` as scoped |
| `QueueEntryDto` | MODIFY | Add `IsWalkIn` boolean flag and `AppointmentType` field for "Walk-In" label on dashboard |

---

## Implementation Plan

1. **Create `WalkIn` domain entity** in `Scheduling/Domain/Entities/WalkIn.cs`: properties `WalkInId` (Guid PK), `PatientName` (string, required), `Phone` (string, optional), `VisitReason` (string, required), `PatientId` (Guid?, FK to `Patients`), `AppointmentId` (Guid?, FK to `Appointments`), `IsConverted` (bool, default false), `CreatedAt` (DateTimeOffset), `CreatedBy` (Guid, FK to `Users`).
2. **Extend `AppointmentType` enum** in `Scheduling/Domain/Enums/AppointmentType.cs`: add `WalkIn` value alongside existing values (`Scheduled`, etc.).
3. **Create DTOs** in `Scheduling/DTOs/`: `CreateWalkinRequest` (with `[Required]` on `PatientName` and `VisitReason`, `[MaxLength(200)]` on `PatientName`, `[MaxLength(500)]` on `VisitReason`, `[RegularExpression]` on `Phone`), `WalkinResponse`, `ConvertWalkinRequest`, `ConvertWalkinResponse`, `PatientSearchResultDto`.
4. **Create `IPatientSearchService`** and **`PatientSearchService`**: query `Patients` table with `EF.Functions.ILike` on `FirstName`, `LastName`, and `Phone` fields. Return top 10 matches ordered by `LastName, FirstName`. Accept `string query` parameter and search across both name (concatenated first + last) and phone. Apply `[Authorize(Roles = "Staff,Admin")]`.
5. **Create `IWalkinService`** and **`WalkinService`**:
   - `CreateWalkinAsync`: Validate input. If `ExistingPatientId` provided, verify patient exists (return `404` if not). Create `WalkIn` record. Create `Appointment` with `Type = WalkIn`, `Status = Waiting`, `Reason = VisitReason`, `DateTime = DateTimeOffset.UtcNow`. If `ConvertToPatient` is true and `ExistingPatientId` is null, create a new `Patient` record and `User` account, then link `WalkIn.PatientId`. Calculate queue position via `IWaitTimeEstimationService`. Check capacity: query today's queue count against `WalkIn:CapacityThreshold` config; set `IsAtCapacity` flag in response. Invalidate Redis cache key `queue:today:*` after insert.
   - `ConvertWalkinAsync`: Find `WalkIn` by ID (return `404` if not found). Verify `IsConverted == false` (return `409 Conflict` if already converted). Create `Patient` + `User` records from provided demographics. Link `WalkIn.PatientId` and `Appointment.PatientId`. Set `WalkIn.IsConverted = true`. Return `ConvertWalkinResponse`.
6. **Create `WalkinController`** at route `api/v1/walkins`: `POST /` delegates to `IWalkinService.CreateWalkinAsync`; `POST /{id}/convert` delegates to `IWalkinService.ConvertWalkinAsync`. Apply `[Authorize(Roles = "Staff,Admin")]`.
7. **Create `PatientSearchController`** at route `api/v1/patients`: `GET /search?q={query}` delegates to `IPatientSearchService.SearchAsync`. Apply `[Authorize(Roles = "Staff,Admin")]`. Validate `q` minimum length of 2 characters; return `400` otherwise.
8. **Modify `QueueEntryDto`**: add `IsWalkIn` (bool) and `AppointmentType` (string) properties. Update `QueueService` projection to populate these from `Appointment.Type == AppointmentType.WalkIn`.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Controllers/
│   │   │   ├── QueueController.cs              ← EXISTS (us_031/task_002)
│   │   │   ├── WalkinController.cs             ← CREATE
│   │   │   └── PatientSearchController.cs      ← CREATE
│   │   ├── Services/
│   │   │   ├── IQueueService.cs                ← EXISTS (us_031/task_002)
│   │   │   ├── QueueService.cs                 ← MODIFY (add IsWalkIn projection)
│   │   │   ├── IWalkinService.cs               ← CREATE
│   │   │   ├── WalkinService.cs                ← CREATE
│   │   │   ├── IPatientSearchService.cs        ← CREATE
│   │   │   └── PatientSearchService.cs         ← CREATE
│   │   ├── Domain/
│   │   │   ├── Entities/
│   │   │   │   └── WalkIn.cs                   ← CREATE
│   │   │   └── Enums/
│   │   │       ├── QueueState.cs               ← EXISTS (us_031/task_002)
│   │   │       └── AppointmentType.cs          ← MODIFY (add WalkIn value)
│   │   └── DTOs/
│   │       ├── QueueEntryDto.cs                ← MODIFY (add IsWalkIn, AppointmentType)
│   │       ├── CreateWalkinRequest.cs           ← CREATE
│   │       ├── WalkinResponse.cs               ← CREATE
│   │       ├── ConvertWalkinRequest.cs          ← CREATE
│   │       ├── ConvertWalkinResponse.cs         ← CREATE
│   │       └── PatientSearchResultDto.cs        ← CREATE
│   └── [existing modules...]
├── Program.cs                                   ← MODIFY (DI registration)
└── appsettings.json                             ← MODIFY (add WalkIn:CapacityThreshold)
```

> Placeholder: Update this tree after us_031 tasks are complete and the actual Scheduling module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Scheduling/Controllers/WalkinController.cs` | `POST /api/v1/walkins` and `POST /api/v1/walkins/{id}/convert` endpoints |
| CREATE | `Server/Modules/Scheduling/Controllers/PatientSearchController.cs` | `GET /api/v1/patients/search?q=` endpoint |
| CREATE | `Server/Modules/Scheduling/Services/IWalkinService.cs` | Interface for walk-in creation and conversion |
| CREATE | `Server/Modules/Scheduling/Services/WalkinService.cs` | Business logic: walk-in CRUD, queue insertion, capacity check, patient conversion |
| CREATE | `Server/Modules/Scheduling/Services/IPatientSearchService.cs` | Interface for patient search |
| CREATE | `Server/Modules/Scheduling/Services/PatientSearchService.cs` | EF Core query with ILike on name and phone |
| CREATE | `Server/Modules/Scheduling/Domain/Entities/WalkIn.cs` | Walk-in domain entity |
| CREATE | `Server/Modules/Scheduling/DTOs/CreateWalkinRequest.cs` | Request DTO with validation attributes |
| CREATE | `Server/Modules/Scheduling/DTOs/WalkinResponse.cs` | Response DTO with queue position and capacity flag |
| CREATE | `Server/Modules/Scheduling/DTOs/ConvertWalkinRequest.cs` | Conversion request with demographics |
| CREATE | `Server/Modules/Scheduling/DTOs/ConvertWalkinResponse.cs` | Conversion response with patient ID |
| CREATE | `Server/Modules/Scheduling/DTOs/PatientSearchResultDto.cs` | Search result DTO with name, DOB, phone |
| MODIFY | `Server/Modules/Scheduling/Domain/Enums/AppointmentType.cs` | Add `WalkIn` enum value |
| MODIFY | `Server/Modules/Scheduling/DTOs/QueueEntryDto.cs` | Add `IsWalkIn` and `AppointmentType` properties |
| MODIFY | `Server/Modules/Scheduling/Services/QueueService.cs` | Populate `IsWalkIn` flag in queue entry projection |
| MODIFY | `Server/Program.cs` | Register `IWalkinService`, `IPatientSearchService` as scoped |
| MODIFY | `Server/appsettings.json` | Add `"WalkIn": { "CapacityThreshold": 50 }` configuration |

---

## External References

- ASP.NET Core 8 Web API controller routing: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0
- EF Core 8 `EF.Functions.ILike` for case-insensitive search: https://learn.microsoft.com/en-us/ef/core/providers/npgsql/functions
- ASP.NET Core 8 model validation with `[ApiController]`: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#automatic-http-400-responses
- ASP.NET Core 8 role-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- ASP.NET Core 8 `IDistributedCache` invalidation: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0
- FR-SO-003: Walk-in creation, queue insertion, and conversion of walk-ins to registered patients

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

- [ ] Unit tests pass for `WalkinService` (mock EF context, mock `IWaitTimeEstimationService`, mock `IDistributedCache`)
- [ ] Unit tests pass for `PatientSearchService` (mock EF context)
- [ ] Integration tests pass for `POST /api/v1/walkins` returning `201` with queue position
- [ ] Integration tests pass for `GET /api/v1/patients/search?q=` returning matching patients
- [ ] Integration tests pass for `POST /api/v1/walkins/{id}/convert` returning `200` with patient ID
- [ ] Authorization verified: unauthenticated requests return `401`; Patient role returns `403`
- [ ] Capacity warning flag returned when queue count exceeds threshold
- [ ] Redis cache invalidated after walk-in insertion

---

## Implementation Checklist

- [ ] Create `WalkIn` domain entity with required properties and FK relationships
- [ ] Extend `AppointmentType` enum with `WalkIn` value
- [ ] Create request/response DTOs with validation attributes (`[Required]`, `[MaxLength]`, `[RegularExpression]`)
- [ ] Implement `PatientSearchService` with `EF.Functions.ILike` on name and phone fields; limit results to 10
- [ ] Implement `WalkinService.CreateWalkinAsync` — persist walk-in, create appointment, calculate queue position, check capacity
- [ ] Implement `WalkinService.ConvertWalkinAsync` — create patient and user records, link to walk-in, return conversion status
- [ ] Create `WalkinController` and `PatientSearchController` with `[Authorize(Roles = "Staff,Admin")]`
- [ ] Modify `QueueEntryDto` and `QueueService` to include `IsWalkIn` flag for dashboard labelling

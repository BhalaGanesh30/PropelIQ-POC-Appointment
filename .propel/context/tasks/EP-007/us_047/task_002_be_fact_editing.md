---
task_id: task_002
user_story: us_047
epic: EP-007
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_047] Authorized Data Editing and Verification
- **Story Location**: [.propel/context/tasks/EP-007/us_047/us_047.md](.propel/context/tasks/EP-007/us_047/us_047.md)
- **Acceptance Criteria**:
  - AC-1: Given an authorized clinician edits a fact, When PATCH is called, Then the change is saved, `verified = true`, and an audit record is written with the previous value.
  - AC-2: Given a clinician marks a fact as verified, When POST /verify is called, Then `verified = true`, `verified_by`, and `verified_at` are persisted and audited.
  - AC-3: Given an audit trail exists, When GET /history is called, Then previous values, editors, and timestamps are returned in chronological order.
  - AC-4: Given a patient role calls PATCH, When the request is received, Then HTTP 403 is returned and no change is persisted.
- **Edge Cases**:
  - Edge Case 1: Two clinicians edit simultaneously — optimistic concurrency via `If-Match` ETag (mapped to `row_version`); if version mismatch, return HTTP 409 with current fact value.
  - Edge Case 2: Fact referenced by a coding decision — edit is allowed; response includes `referencedByCodingDecision: true` flag for FE to display the warning.

---

## Design References (Backend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A (backend task) |
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
| Observability | OpenTelemetry | latest |
| Frontend | N/A | N/A |
| AI/ML | N/A | N/A |
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

Implement three endpoints in the `ClinicalIntelligence` module to support authorized fact editing and verification:

1. `PATCH /api/v1/clinical-facts/{id}` — edits `name` and/or `value` on an existing `clinical_facts` row. Reads the `If-Match` request header and compares it against the stored `row_version` to enforce optimistic concurrency control (Edge Case 1). On version match: update `name`, `value`, set `verified = true`, `verified_by = currentUserId`, `verified_at = now()`, increment `row_version`, write an `AUDIT_RECORD` with `event_type = "fact_edited"` capturing `previousName`, `previousValue`, and `editorId` in the `details` JSONB. On version mismatch: return HTTP 409 with the current fact as the response body. Check for referencing `CODING_DECISION` rows; if found, set `referencedByCodingDecision = true` in the response (Edge Case 2). Return `HTTP 200` with the updated `ClinicalFactDto` (including the new ETag from `row_version`) on success.

2. `POST /api/v1/clinical-facts/{id}/verify` — marks a fact as verified without changing its content. Sets `verified = true`, `verified_by = currentUserId`, `verified_at = now()`, increments `row_version`, writes an `AUDIT_RECORD` with `event_type = "fact_verified"`. Returns HTTP 200 with the updated fact.

3. `GET /api/v1/clinical-facts/{id}/history` — returns audit records for the fact in chronological order by `created_at`. Queries `audit_records` where `entity_type = 'clinical_fact'` and `entity_id = factId`. Returns a list of `FactAuditEntryDto` (auditId, eventType, previousValue, editorDisplayName, timestamp). Access: Clinician and Staff (read-only history view).

All write endpoints enforce `[Authorize(Roles = "Clinician")]`. The history endpoint allows `Clinician` and `Staff`. Patient role returns HTTP 403 on write endpoints (AC-4).

---

## Dependent Tasks

- **us_047/task_003** — `row_version` and `updated_at` columns must be added to `clinical_facts` before optimistic concurrency can be implemented.
- **us_044/task_002** — `clinical_facts` table base schema must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ClinicalFactsController` | CREATE | PATCH /{id}, POST /{id}/verify, GET /{id}/history |
| `IFactEditingService` | CREATE | Interface: `EditAsync`, `VerifyAsync`, `GetHistoryAsync` |
| `FactEditingService` | CREATE | Orchestrates: concurrency check → update → audit → coding decision check |
| `PatchFactRequest` | CREATE | DTO: `string? Name`, `string? Value` |
| `VerifyFactResponse` | CREATE | DTO: updated `ClinicalFactDto` |
| `FactAuditEntryDto` | CREATE | DTO: `auditId`, `eventType`, `previousValue`, `editorDisplayName`, `timestamp` |
| `ClinicalFactResponseDto` | CREATE | Extended fact DTO: all fields + `etag` (from row_version) + `referencedByCodingDecision` flag |
| `IClinicalFactRepository` | MODIFY | Add: `GetByIdAsync`, `UpdateAsync` (with row_version check), `GetRowVersionAsync` |
| `ClinicalFactRepository` | MODIFY | Implement: optimistic update using `WHERE fact_id = @id AND row_version = @version`; return rows affected |
| `IAuditRepository` | CREATE | Interface: `AddAsync(AuditRecord)`, `GetByEntityAsync(entityType, entityId)` |
| `AuditRepository` | CREATE | EF Core implementation querying `audit_records` |
| `ICodingDecisionRepository` | CREATE | Interface: `ExistsForFactAsync(Guid factId)` |
| `CodingDecisionRepository` | CREATE | EF Core: `audit_records` or `coding_decisions` — check for `fact_id = factId` |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new services and repositories |

---

## Implementation Plan

1. **Create `PatchFactRequest` DTO**: `string? Name` (max 255 chars), `string? Value`. At least one field must be non-null — enforce with model validation attribute or `[CustomValidation]`.
2. **Create `ClinicalFactResponseDto`**: All `ClinicalFactDto` fields from US_045 plus `string ETag` (string representation of `row_version`), `bool ReferencedByCodingDecision`. Add `ETag` response header set to `row_version.ToString()` on all successful write responses.
3. **Create `FactAuditEntryDto`**: `Guid AuditId`, `string EventType`, `string PreviousValue`, `string EditorDisplayName`, `DateTimeOffset Timestamp`.
4. **Extend `IClinicalFactRepository`**:
   - `GetByIdAsync(Guid factId, CancellationToken ct)` — returns `ClinicalFact?` with current state.
   - `UpdateAsync(ClinicalFact fact, int expectedRowVersion, CancellationToken ct)` — executes: `UPDATE clinical_facts SET name = @name, value = @value, verified = @verified, verified_by = @verifiedBy, updated_at = now(), row_version = row_version + 1 WHERE fact_id = @id AND row_version = @expectedVersion; SELECT ROW_COUNT()`. Returns `true` if one row updated (version matched), `false` if zero rows updated (version mismatch). Use EF Core raw SQL or `ExecuteSqlRawAsync` for atomic update-with-version-check pattern.
5. **Create `IFactEditingService` and `FactEditingService`**:
   - `EditAsync(Guid factId, PatchFactRequest request, int expectedRowVersion, Guid editorId, CancellationToken ct)`:
     - (a) Fetch current fact via `GetByIdAsync`; return `null` → HTTP 404.
     - (b) Capture `previousName = fact.Name`, `previousValue = fact.Value`.
     - (c) Apply changes: `fact.Name = request.Name ?? fact.Name`, `fact.Value = request.Value ?? fact.Value`, `fact.Verified = true`, `fact.VerifiedBy = editorId`, `fact.VerifiedAt = DateTimeOffset.UtcNow`.
     - (d) Call `UpdateAsync(fact, expectedRowVersion)`. If `false` (version mismatch): fetch current fact, return `EditResult.Conflict(currentFact)`.
     - (e) Write `AUDIT_RECORD`: `event_type = "fact_edited"`, `entity_type = "clinical_fact"`, `entity_id = factId`, `details = { previousName, previousValue, newName, newValue, editorId }`.
     - (f) Check `ICodingDecisionRepository.ExistsForFactAsync(factId)`; set `referencedByCodingDecision` accordingly.
     - (g) Return `EditResult.Success(updatedFact, referencedByCodingDecision)`.
   - `VerifyAsync(Guid factId, Guid verifierId, CancellationToken ct)`: fetch fact → set `verified = true`, `verified_by = verifierId`, `verified_at = now()` → update (no concurrency header required for verify-only) → write `AUDIT_RECORD` with `event_type = "fact_verified"` and `details = { verifierId, previousVerified = false }`.
   - `GetHistoryAsync(Guid factId, CancellationToken ct)`: query `audit_records` via `IAuditRepository.GetByEntityAsync("clinical_fact", factId)` ordered by `created_at ASC`. Join on `users` to get `EditorDisplayName` from `details.editorId`. Map to `FactAuditEntryDto` list.
6. **Create `ClinicalFactsController`**:
   - `[HttpPatch("{id}")]`: parse `If-Match` header → extract `row_version` as integer. If header missing, return HTTP 428 (Precondition Required). Call `FactEditingService.EditAsync()`. On `Conflict` result: return HTTP 409 with current fact body. On `Success` result: set `ETag: {newRowVersion}` response header, return HTTP 200 with `ClinicalFactResponseDto`.
   - `[HttpPost("{id}/verify")]`: call `FactEditingService.VerifyAsync()`. Return HTTP 200 with updated fact. Set ETag header.
   - `[HttpGet("{id}/history")]`: call `FactEditingService.GetHistoryAsync()`. Return HTTP 200 with `List<FactAuditEntryDto>`.
   - Authorization: PATCH and POST/verify → `[Authorize(Roles = "Clinician")]`. GET/history → `[Authorize(Roles = "Clinician,Staff")]`.
7. **Create `IAuditRepository` and `AuditRepository`**: `AddAsync(AuditRecord record)` — EF Core insert into `audit_records`. `GetByEntityAsync(string entityType, Guid entityId)` — EF Core query with `Where` + `OrderBy(a => a.CreatedAt)`, join user display names via navigation property.
8. **Create `ICodingDecisionRepository` and `CodingDecisionRepository`**: `ExistsForFactAsync(Guid factId)` — `AnyAsync(cd => cd.FactId == factId)` against `coding_decisions` table.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── DocumentsController.cs            ← EXISTS (US_040-US_043)
│   │   │   ├── PatientProfileController.cs       ← EXISTS (US_045)
│   │   │   ├── ConflictController.cs             ← EXISTS (US_046)
│   │   │   └── ClinicalFactsController.cs        ← CREATE
│   │   ├── Services/
│   │   │   ├── IClinicalExtractionService.cs     ← EXISTS (US_044)
│   │   │   ├── IPatientProfileAggregationService.cs  ← EXISTS (US_045)
│   │   │   ├── IConflictDetectionService.cs      ← EXISTS (US_046)
│   │   │   ├── IFactEditingService.cs            ← CREATE
│   │   │   └── FactEditingService.cs             ← CREATE
│   │   ├── DTOs/
│   │   │   ├── ClinicalFactDto.cs                ← EXISTS (US_045)
│   │   │   ├── ClinicalFactResponseDto.cs        ← CREATE (extends ClinicalFactDto with ETag, referencedByCodingDecision)
│   │   │   ├── PatchFactRequest.cs               ← CREATE
│   │   │   ├── VerifyFactResponse.cs             ← CREATE
│   │   │   └── FactAuditEntryDto.cs              ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalFactRepository.cs        ← MODIFY (add GetByIdAsync, UpdateAsync with version check)
│   │   │   ├── ClinicalFactRepository.cs         ← MODIFY (implement atomic optimistic update)
│   │   │   ├── IAuditRepository.cs               ← CREATE
│   │   │   ├── AuditRepository.cs                ← CREATE
│   │   │   ├── ICodingDecisionRepository.cs      ← CREATE
│   │   │   └── CodingDecisionRepository.cs       ← CREATE
│   │   └── [existing module structure...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/ClinicalFactsController.cs` | PATCH /{id}, POST /{id}/verify (Clinician), GET /{id}/history (Clinician+Staff) |
| CREATE | `Modules/ClinicalIntelligence/Services/IFactEditingService.cs` | Editing service interface |
| CREATE | `Modules/ClinicalIntelligence/Services/FactEditingService.cs` | Concurrency check, update, audit write, coding decision check |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ClinicalFactResponseDto.cs` | Extended DTO with ETag and referencedByCodingDecision |
| CREATE | `Modules/ClinicalIntelligence/DTOs/PatchFactRequest.cs` | Edit request: nullable Name and Value |
| CREATE | `Modules/ClinicalIntelligence/DTOs/FactAuditEntryDto.cs` | History entry: auditId, eventType, previousValue, editorDisplayName, timestamp |
| CREATE | `Modules/ClinicalIntelligence/Repositories/IAuditRepository.cs` | AddAsync, GetByEntityAsync |
| CREATE | `Modules/ClinicalIntelligence/Repositories/AuditRepository.cs` | EF Core audit_records CRUD |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ICodingDecisionRepository.cs` | ExistsForFactAsync |
| CREATE | `Modules/ClinicalIntelligence/Repositories/CodingDecisionRepository.cs` | EF Core coding_decisions query |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalFactRepository.cs` | Add GetByIdAsync, UpdateAsync with row_version check |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalFactRepository.cs` | Atomic optimistic update via ExecuteSqlRawAsync |

---

## External References

- HTTP ETag / If-Match: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/If-Match
- HTTP 409 Conflict: https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/409
- HTTP 428 Precondition Required: https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/428
- EF Core raw SQL: https://learn.microsoft.com/en-us/ef/core/querying/sql-queries
- FR-CA-004: Authorized staff may edit and verify extracted data with immutable audit history
- NFR-010: Immutable audit evidence — 7-year retention; append-only write constraints
- DR-003: Clinical fields must store verification state and last reviewer metadata

---

## Build Commands

```bash
# Restore packages
dotnet restore

# Build solution
dotnet build --no-restore

# Run unit tests
dotnet test --no-build --filter "Category=Unit"

# Run integration tests
dotnet test --no-build --filter "Category=Integration"

# Run the API
dotnet run --project src/Api/Api.csproj
```

---

## Implementation Validation Strategy

- [ ] `PATCH /api/v1/clinical-facts/{id}` with matching `If-Match` ETag returns HTTP 200 with updated fact; `verified = true`, `verified_by` set (AC-1)
- [ ] PATCH response includes `ETag` header with new `row_version` value
- [ ] PATCH without `If-Match` header returns HTTP 428
- [ ] Concurrent edit with stale `If-Match` returns HTTP 409 with current fact body (Edge Case 1)
- [ ] PATCH by Patient role returns HTTP 403 (AC-4)
- [ ] `POST /{id}/verify` returns HTTP 200; `verified = true`, `verified_by`, `verified_at` set in DB (AC-2)
- [ ] After PATCH: `audit_records` row written with `event_type="fact_edited"`, `entity_type="clinical_fact"`, `details` containing `previousName`/`previousValue`/`editorId` (AC-1)
- [ ] After POST/verify: `audit_records` row written with `event_type="fact_verified"` (AC-2)
- [ ] `GET /{id}/history` returns audit entries ordered chronologically by `created_at` ASC with editor display names (AC-3)
- [ ] PATCH response includes `referencedByCodingDecision: true` when a coding decision references the fact (Edge Case 2)
- [ ] Unauthenticated callers receive HTTP 401 on all three endpoints

---

## Implementation Checklist

- [ ] Create `PatchFactRequest`, `ClinicalFactResponseDto` (with ETag), `FactAuditEntryDto` DTOs
- [ ] Extend `IClinicalFactRepository` / `ClinicalFactRepository` with `GetByIdAsync` and atomic `UpdateAsync` using `row_version` (Edge Case 1)
- [ ] Create `IAuditRepository` / `AuditRepository` for `audit_records` insert and query by entity
- [ ] Create `ICodingDecisionRepository` / `CodingDecisionRepository` with `ExistsForFactAsync` (Edge Case 2)
- [ ] Create `IFactEditingService` / `FactEditingService`: edit (concurrency check → update → audit → coding decision flag), verify, get history
- [ ] Create `ClinicalFactsController`: PATCH (Clinician, If-Match required), POST/verify (Clinician), GET/history (Clinician+Staff); set ETag response header on writes
- [ ] Register all new services and repositories in `ClinicalIntelligenceModule` DI
- [ ] Verify HTTP 403 for Patient role, HTTP 428 for missing If-Match, HTTP 409 for version mismatch (AC-4, Edge Case 1)

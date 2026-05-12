---
task_id: task_002
user_story: us_051
epic: EP-008
layer: Backend
status: completed
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_051] Accept, Modify, and Reject Coding Workflow
- **Story Location**: [.propel/context/tasks/EP-008/us_051/us_051.md](.propel/context/tasks/EP-008/us_051/us_051.md)
- **Acceptance Criteria**:
  - AC-1: Given coding suggestions are displayed, When I click "Accept" on a suggestion, Then the code is recorded as the finalized coding decision with my identity, the accepted code, and the AI suggestion it was accepted from stored in the audit trail.
  - AC-2: Given I want to modify a suggestion, When I click "Modify" and update the code or description, Then the modified code is saved as the finalized decision with a "Modified from AI suggestion" audit record including the original and final values.
  - AC-3: Given I reject a suggestion, When I click "Reject" (after confirming the confirmation dialog), Then the suggestion is marked as rejected in the audit trail.
  - AC-4: Given I have not made a decision on all required codes, When I attempt to submit the encounter for billing, Then the system blocks submission with "Coding decisions required" and lists the pending items.
- **Edge Cases**:
  - Edge Case 1: Edit Decision after accept — `reviewer_action` may be updated from `accepted` to `modified` (or back to `accepted`) while encounter is in `pending` status; blocked once `encounter_status = submitted`.
  - Edge Case 2: Agreement rate tracking — each accept/modify/reject action persists `reviewer_action` to `coding_decisions`; agreement rate is derived by the monitoring dashboard querying `coding_decisions WHERE reviewer_action = 'accepted'` as a percentage of all decided rows (AIR-007).

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
| Cache | Redis (StackExchange.Redis) | 2.x |
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

Implement the Accept, Modify, and Reject endpoints in the `ClinicalIntelligence` module. All three transitions update a `coding_decisions` row and write an immutable `audit_records` entry (NFR-010). The `coding_decisions` table (US_049/task_003) already has `reviewer_action`, `reviewer_id`, and `decided_at` columns; US_051/task_003 adds `original_icd10_code` and `original_cpt_code` columns for agreement rate tracking (AIR-007).

Endpoints:
- `POST /api/v1/coding-decisions/{id}/accept` — sets `reviewer_action = accepted`, `reviewer_id = caller`, `decided_at = now()`. Audits `event_type = "coding_accepted"` with `original_ai_code` and `final_code` (same value) in audit `details` JSONB (AC-1).
- `PATCH /api/v1/coding-decisions/{id}/modify` — validates `finalCode` is non-empty; sets `reviewer_action = modified`, copies existing `icd10_code`/`cpt_code` into `original_icd10_code`/`original_cpt_code` before overwriting with final values (task_003 columns); audits `event_type = "coding_modified"` with `original_value` and `final_value` in JSONB (AC-2, NFR-010).
- `POST /api/v1/coding-decisions/{id}/reject` — sets `reviewer_action = rejected`, `reviewer_id = caller`, `decided_at = now()`. Audits `event_type = "coding_rejected"` (AC-3, NFR-010).
- `GET /api/v1/patients/{patientId}/coding-decisions/pending` — returns list of all `coding_decisions WHERE patient_id = :patientId AND reviewer_action = 'pending'`; used by FE to populate AC-4 submission block banner.

Guard: All three mutation endpoints enforce `encounter_status != 'submitted'` — return HTTP 409 "Encounter already submitted; use amendment workflow" if violated (Edge Case 1). Clinician role only.

Redis: Invalidate the `GET /api/v1/patients/{id}/coding-suggestions` (ICD-10) and `GET /api/v1/patients/{id}/coding-decisions/pending` cache entries on any successful mutation to keep FE state consistent.

---

## Dependent Tasks

- **us_051/task_003** — `original_icd10_code` and `original_cpt_code` columns required on `coding_decisions` for the Modify endpoint's original-value snapshot.
- **us_049/task_003** — `coding_decisions` table and `reviewer_action_enum` must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CodingDecisionController` | CREATE | POST accept, PATCH modify, POST reject, GET pending; Clinician-only |
| `ICodingDecisionWorkflowService` | CREATE | Interface: `AcceptAsync`, `ModifyAsync`, `RejectAsync`, `GetPendingAsync` |
| `CodingDecisionWorkflowService` | CREATE | Orchestrates: load decision → guard check → update row → audit write → cache invalidation |
| `CodingDecisionGuard` | CREATE | Validates `encounter_status != submitted`; returns HTTP 409 if blocked (Edge Case 1) |
| `ICodingDecisionRepository` | MODIFY | Add: `UpdateReviewerActionAsync`, `GetPendingByPatientAsync`; extend existing interface (US_049) |
| `CodingDecisionRepository` | MODIFY | Implement: atomic UPDATE for reviewer fields; query for pending by patient |
| `AcceptDecisionRequestDto` | CREATE | Empty body (decision ID is path param; identity from auth context) |
| `ModifyDecisionRequestDto` | CREATE | `string FinalCode` (required, max 20), `string FinalDescription` (required) |
| `PendingDecisionDto` | CREATE | `{ decisionId, icdCode, cptCode, patientId, createdAt }` — returned by GET pending |
| `AuditService` | REUSE | US_044 — `LogAsync(event_type, entity_type, entity_id, details JSONB)` |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new service and controller |

---

## Implementation Plan

1. **Create request/response DTOs**: `AcceptDecisionRequestDto` (empty — decision ID from path, reviewer from `HttpContext.User`); `ModifyDecisionRequestDto` (`string FinalCode` required max 20, `string FinalDescription` required); `PendingDecisionDto` (`Guid DecisionId`, `string? IcdCode`, `string? CptCode`, `Guid PatientId`, `DateTimeOffset CreatedAt`).
2. **Extend `ICodingDecisionRepository`**: Add `UpdateReviewerActionAsync(Guid decisionId, ReviewerAction action, Guid reviewerId, string? finalCode, string? finalDescription, string? originalCode, CancellationToken ct)` — performs atomic `UPDATE coding_decisions SET ... WHERE decision_id = :id AND reviewer_action = 'pending'` (prevents double-accept); returns number of rows affected (0 = already decided or not found → HTTP 409). Add `GetPendingByPatientAsync(Guid patientId, CancellationToken ct): Task<List<PendingDecisionDto>>`.
3. **Create `CodingDecisionGuard`**: Inject `IEncounterStatusRepository` (or query `appointments` table by `patient_id`). Check that the patient's active encounter is not in `submitted` status. If submitted → throw `EncounterAlreadySubmittedException` (HTTP 409). Called at the start of all three mutation operations.
4. **Create `ICodingDecisionWorkflowService` / `CodingDecisionWorkflowService`**:
   - `AcceptAsync(Guid decisionId, Guid reviewerId, CancellationToken ct)`: guard → `UpdateReviewerActionAsync(accepted, reviewerId, null, null, null)` → audit `event_type = "coding_accepted"` with `{ decision_id, final_code }` JSONB (AC-1, NFR-010).
   - `ModifyAsync(Guid decisionId, ModifyDecisionRequestDto req, Guid reviewerId, CancellationToken ct)`: guard → load current decision to snapshot `original_code` → `UpdateReviewerActionAsync(modified, reviewerId, req.FinalCode, req.FinalDescription, originalCode)` — `original_icd10_code`/`original_cpt_code` populated here (task_003 columns) → audit `event_type = "coding_modified"` with `{ original_value, final_value }` JSONB (AC-2, NFR-010).
   - `RejectAsync(Guid decisionId, Guid reviewerId, CancellationToken ct)`: guard → `UpdateReviewerActionAsync(rejected, reviewerId, null, null, null)` → audit `event_type = "coding_rejected"` with `{ decision_id }` JSONB (AC-3, NFR-010).
   - Each method invalidates Redis keys: `suggestions:{patientId}:*` and `pending:{patientId}`.
5. **Create `CodingDecisionController`**: `[HttpPost("{id}/accept")]`, `[HttpPatch("{id}/modify")]`, `[HttpPost("{id}/reject")]` — all `[Authorize(Roles = "Clinician")]`. Validate `id` as Guid. Extract `reviewerId` from `HttpContext.User`. Delegate to `CodingDecisionWorkflowService`. Return HTTP 200 with `PendingDecisionDto` response on success; HTTP 409 on guard failure or already-decided state; HTTP 422 on invalid `id`. Add `[HttpGet("patients/{patientId}/coding-decisions/pending")]` returning `List<PendingDecisionDto>` (AC-4).
6. **OpenTelemetry instrumentation**: Activity span per operation; tags: `decision.id`, `reviewer_action`, `patient.id`; counter `coding_decision.accept_count`, `coding_decision.modify_count`, `coding_decision.reject_count` — feeds AIR-007 monitoring dashboard (Edge Case 2).

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── CodingSuggestionController.cs         ← EXISTS (US_049)
│   │   │   ├── CptSuggestionController.cs            ← EXISTS (US_050)
│   │   │   └── CodingDecisionController.cs           ← CREATE
│   │   ├── Services/
│   │   │   ├── CodingSuggestionOrchestrator.cs       ← EXISTS (US_049)
│   │   │   ├── ICodingDecisionWorkflowService.cs     ← CREATE
│   │   │   ├── CodingDecisionWorkflowService.cs      ← CREATE
│   │   │   └── CodingDecisionGuard.cs                ← CREATE
│   │   ├── Repositories/
│   │   │   ├── ICodingDecisionRepository.cs          ← MODIFY (add UpdateReviewerActionAsync, GetPendingByPatientAsync)
│   │   │   └── CodingDecisionRepository.cs           ← MODIFY (implement new methods)
│   │   ├── DTOs/
│   │   │   ├── ModifyDecisionRequestDto.cs           ← CREATE
│   │   │   └── PendingDecisionDto.cs                 ← CREATE
│   │   └── [existing module structure...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/CodingDecisionController.cs` | POST accept, PATCH modify, POST reject, GET pending; Clinician-only; HTTP 409 on guard failure |
| CREATE | `Modules/ClinicalIntelligence/Services/ICodingDecisionWorkflowService.cs` | Workflow service interface |
| CREATE | `Modules/ClinicalIntelligence/Services/CodingDecisionWorkflowService.cs` | Orchestrates guard → update → audit → cache invalidation for all three actions |
| CREATE | `Modules/ClinicalIntelligence/Services/CodingDecisionGuard.cs` | Encounter submission status guard; HTTP 409 on violation (Edge Case 1) |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ModifyDecisionRequestDto.cs` | FinalCode (required, max 20), FinalDescription (required) |
| CREATE | `Modules/ClinicalIntelligence/DTOs/PendingDecisionDto.cs` | decisionId, icdCode, cptCode, patientId, createdAt |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ICodingDecisionRepository.cs` | Add UpdateReviewerActionAsync (atomic UPDATE WHERE reviewer_action = pending), GetPendingByPatientAsync |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/CodingDecisionRepository.cs` | Implement new repository methods; populate original_icd10_code/original_cpt_code on modify |

---

## External References

- EF Core optimistic concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- OpenTelemetry counters: https://opentelemetry.io/docs/languages/dotnet/instrumentation/#creating-metrics
- NFR-010: Immutable audit evidence for coding decisions — 7-year retention
- AIR-007: Agreement rate ≥ 98% against clinician-reviewed benchmark; tracked via accept/modify/reject counters
- AIR-005: Human-in-the-loop mandatory for all coding decisions (AC-1, AC-2, AC-3)
- FR-MC-003 [HYBRID]: User decision required before finalization; blocks billing submission (AC-4)
- DR-005: Audit logs retained 7 years; coding decision audit records must satisfy this requirement

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

- [X] `POST /{id}/accept` sets `reviewer_action = accepted`, `reviewer_id`, `decided_at`; audit record `coding_accepted` written with `final_code` (AC-1, NFR-010)
- [X] `PATCH /{id}/modify` sets `reviewer_action = modified`; `original_icd10_code`/`original_cpt_code` populated from pre-modification values; audit record `coding_modified` with `original_value` and `final_value` (AC-2, NFR-010, AIR-007)
- [X] `POST /{id}/reject` sets `reviewer_action = rejected`; audit record `coding_rejected` written (AC-3, NFR-010)
- [X] `UpdateReviewerActionAsync` atomically updates only rows where `reviewer_action = 'pending'`; returns 0 rows if already decided → HTTP 409
- [X] All three mutation endpoints return HTTP 409 when `encounter_status = submitted` (Edge Case 1)
- [X] `GET /patients/{patientId}/coding-decisions/pending` returns only `reviewer_action = 'pending'` rows for the given patient (AC-4)
- [X] OpenTelemetry counters `coding_decision.accept_count`, `coding_decision.modify_count`, `coding_decision.reject_count` emitted per action (AIR-007, Edge Case 2)
- [X] Redis cache invalidated on each successful mutation; subsequent FE requests fetch fresh suggestion/pending state

---

## Implementation Checklist

- [X] Create `ModifyDecisionRequestDto` (FinalCode max 20 required, FinalDescription required); `PendingDecisionDto`
- [X] Extend `ICodingDecisionRepository` / `CodingDecisionRepository`: `UpdateReviewerActionAsync` (atomic UPDATE WHERE pending); `GetPendingByPatientAsync`
- [X] Create `CodingDecisionGuard`: encounter submission status check; throw `EncounterAlreadySubmittedException` → HTTP 409 (Edge Case 1)
- [X] Create `ICodingDecisionWorkflowService` / `CodingDecisionWorkflowService`: accept/modify/reject orchestration; original-value snapshot on modify (task_003 columns); AuditService writes per action (NFR-010)
- [X] Create `CodingDecisionController`: POST accept, PATCH modify, POST reject (Clinician-only); GET pending; HTTP 409 on guard or double-decision; register in DI
- [X] Add OpenTelemetry counters: `coding_decision.accept_count`, `coding_decision.modify_count`, `coding_decision.reject_count` (AIR-007); cache invalidation for suggestion and pending cache keys

---
task_id: task_002
user_story: us_046
epic: EP-007
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_046] Drug-Drug and Drug-Allergy Conflict Detection
- **Story Location**: [.propel/context/tasks/EP-007/us_046/us_046.md](.propel/context/tasks/EP-007/us_046/us_046.md)
- **Acceptance Criteria**:
  - AC-1: Given clinical facts contain medications and allergies, When the conflict detection engine runs, Then drug-drug and drug-allergy conflicts are identified and classified by severity.
  - AC-2: Given a conflict is detected, When the API responds, Then each conflict alert includes severity label and description.
  - AC-3: Given a Critical conflict exists, When the clinician acknowledges it, Then the acknowledgment is recorded — requires `POST /api/v1/conflicts/{id}/acknowledge`.
  - AC-4: Given the acknowledgment is recorded, When it completes, Then it is logged in the audit trail with clinician identity and timestamp — requires `AuditService` integration.
- **Edge Cases**:
  - Edge Case 1: Rules database outdated — response includes `rulesStale: true` when `conflict_rules.last_updated_at` exceeds configured staleness threshold.
  - Edge Case 2: 20+ medications producing many pairs — conflicts deduplicated to unique drug pairs; only highest severity per pair is returned.

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

Implement the conflict detection API within the `ClinicalIntelligence` module. Two endpoints are required:

1. `GET /api/v1/patients/{id}/conflicts` — loads active medications (fact_type = `medication`) and allergies (fact_type = `allergy`) from `clinical_facts`, evaluates them against rules in `conflict_rules`, and returns a deduplicated, severity-sorted list of conflict alerts. Deduplication retains only the highest severity per drug pair to prevent alert fatigue (Edge Case 2). If any `conflict_rules` row has `last_updated_at` older than the configured staleness threshold (default 30 days), the response includes `rulesStale: true` (Edge Case 1). Results are cached in Redis with a short TTL (30 seconds) since conflict evaluation is dependent on the most recent facts.

2. `POST /api/v1/conflicts/{id}/acknowledge` — records the clinician's acknowledgment on the `conflict_alerts` row (`acknowledged = true`, `acknowledged_by`, `acknowledged_at`), then writes an `AUDIT_RECORD` via `AuditService` with `event_type = "conflict_acknowledged"`, `entity_type = "conflict_alert"`, `entity_id = conflictId`, and `details` containing the clinician's user ID, conflict severity, and timestamp (AC-4, NFR-010). Only `Clinician` role may call this endpoint (SCR-016 access matrix).

The conflict detection engine is deterministic: it normalizes drug and allergy fact names using the same `NormalizationService` from US_044, then performs an in-memory cross-product evaluation of medication-medication pairs (drug-drug) and medication-allergy pairs (drug-allergy) against the `conflict_rules` table.

---

## Dependent Tasks

- **us_046/task_003** — `conflict_alerts` and `conflict_rules` tables must be migrated before this task can be fully integrated.
- **us_044/task_002** — `clinical_facts` table with `fact_type` and `name` columns must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ConflictController` | CREATE | `GET /api/v1/patients/{id}/conflicts` (Clinician+Staff read); `POST /api/v1/conflicts/{id}/acknowledge` (Clinician only) |
| `IConflictDetectionService` | CREATE | Interface: `EvaluateConflictsAsync(Guid patientId, CancellationToken ct)` |
| `ConflictDetectionService` | CREATE | Loads facts + rules, runs cross-product, deduplicates, checks staleness |
| `IConflictRuleRepository` | CREATE | Interface: `GetActiveRulesAsync()`, `GetLastUpdatedAtAsync()` |
| `ConflictRuleRepository` | CREATE | EF Core implementation querying `conflict_rules` |
| `IConflictAlertRepository` | CREATE | Interface: `GetByPatientIdAsync`, `AcknowledgeAsync` |
| `ConflictAlertRepository` | CREATE | EF Core implementation; `AcknowledgeAsync` sets acknowledged + acknowledged_by + acknowledged_at |
| `ConflictAlertDto` | CREATE | Response: conflictId, conflictType, severity, description, drugA, drugB, acknowledged, acknowledgedAt |
| `ConflictAlertsResponseDto` | CREATE | Response: `List<ConflictAlertDto> Alerts`, `bool RulesStale` |
| `ConflictAlert` (EF entity) | CREATE | EF entity mapping to `conflict_alerts` table |
| `ConflictRule` (EF entity) | CREATE | EF entity mapping to `conflict_rules` table |
| `IConflictCacheService` | CREATE | Interface: cache GET conflicts response per patientId |
| `ConflictCacheService` | CREATE | Redis with 30-second TTL; invalidated on acknowledge |
| `IAuditService` | MODIFY | Ensure `LogAsync(AuditRecord)` method exists; used for acknowledgment audit (AC-4, NFR-010) |
| `NormalizationService` | REUSE | From US_044 — normalize drug/allergy names before rule lookup |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new controllers, services, repositories |

---

## Implementation Plan

1. **Create response DTOs**: `ConflictAlertDto` (properties: `Guid ConflictId`, `string ConflictType` ["drug_drug"/"drug_allergy"], `string Severity` ["low"/"moderate"/"high"/"critical"], `string Description`, `string DrugA`, `string DrugB`, `bool Acknowledged`, `DateTimeOffset? AcknowledgedAt`). `ConflictAlertsResponseDto` (`List<ConflictAlertDto> Alerts`, `bool RulesStale`).
2. **Create `IConflictRuleRepository` and `ConflictRuleRepository`**: `GetActiveRulesAsync()` returns all `conflict_rules` where `is_active = true`. `GetLastUpdatedAtAsync()` returns `MAX(last_updated_at)` across the table. Cache result for 5 minutes in memory (rules change infrequently).
3. **Create `IConflictAlertRepository` and `ConflictAlertRepository`**: `GetByPatientIdAsync(Guid patientId)` queries `conflict_alerts` where `patient_id = patientId`. `AcknowledgeAsync(Guid conflictId, Guid clinicianId, DateTimeOffset acknowledgedAt)` updates the row setting `acknowledged = true`, `acknowledged_by = clinicianId`, `acknowledged_at = acknowledgedAt`.
4. **Create `IConflictDetectionService` and `ConflictDetectionService`**: 
   - (a) Load patient's active medication and allergy facts via `IClinicalFactRepository`.
   - (b) Normalize all drug/allergy names using `INormalizationService`.
   - (c) Load active rules via `IConflictRuleRepository.GetActiveRulesAsync()`.
   - (d) **Drug-drug**: cross-product of all medication pairs → match against `conflict_rules` where `rule_type = "drug_drug"` by normalized `(drug_a_name, drug_b_name)` — order-insensitive.
   - (e) **Drug-allergy**: cross-product of medications × allergies → match against `conflict_rules` where `rule_type = "drug_allergy"` by normalized `(drug_a_name, drug_b_name)`.
   - (f) **Deduplication** (Edge Case 2): group matches by `(fact_id_a, fact_id_b)` pair; keep only the row with the highest severity level.
   - (g) For each matched pair, upsert a `conflict_alerts` row (insert if new, skip if already exists for that pair — idempotent).
   - (h) Check staleness: `GetLastUpdatedAtAsync()` vs `DateTime.UtcNow - StalenessThreshold` (configurable, default 30 days); set `RulesStale` flag.
   - (i) Map results to `ConflictAlertDto` list sorted Critical → High → Moderate → Low.
5. **Create `IConflictCacheService` and `ConflictCacheService`**: Cache key: `conflicts:{patientId}`. Serialize `ConflictAlertsResponseDto` as JSON with 30-second TTL. Invalidate on successful `AcknowledgeAsync` by deleting the cache key (so next GET reflects updated acknowledged state).
6. **Create `ConflictController`**:
   - `[HttpGet("patients/{id}/conflicts")]`: validate `id` as Guid. Check cache; on miss call `ConflictDetectionService`, cache result, return. Apply `[Authorize(Roles = "Clinician,Staff")]`. Return `HTTP 200 OK` with `ConflictAlertsResponseDto`.
   - `[HttpPost("conflicts/{id}/acknowledge")]`: validate `id` as Guid. Call `ConflictAlertRepository.AcknowledgeAsync(conflictId, currentUserId, DateTimeOffset.UtcNow)`. Call `AuditService.LogAsync(new AuditRecord { EventType = "conflict_acknowledged", EntityType = "conflict_alert", EntityId = conflictId, Details = { severity, clinicianId, timestamp } })`. Invalidate cache. Return `HTTP 204 No Content`. Apply `[Authorize(Roles = "Clinician")]` — Staff cannot acknowledge (AC-4).
7. **Add OpenTelemetry instrumentation**: Wrap `ConflictDetectionService.EvaluateConflictsAsync` in an Activity. Tags: `patient.id`, `conflicts.total`, `conflicts.critical_count`, `rules.stale`. Metric: `conflict.detection.duration_ms`.
8. **Register services in DI**: `ConflictDetectionService`, `ConflictAlertRepository`, `ConflictRuleRepository`, `ConflictCacheService` in `ClinicalIntelligenceModule`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── DocumentsController.cs            ← EXISTS (US_040-US_043)
│   │   │   ├── PatientProfileController.cs       ← EXISTS (US_045)
│   │   │   └── ConflictController.cs             ← CREATE
│   │   ├── Services/
│   │   │   ├── IClinicalExtractionService.cs     ← EXISTS (US_044)
│   │   │   ├── IPatientProfileAggregationService.cs  ← EXISTS (US_045)
│   │   │   ├── INormalizationService.cs          ← EXISTS (US_044) — REUSE
│   │   │   ├── IConflictDetectionService.cs      ← CREATE
│   │   │   └── ConflictDetectionService.cs       ← CREATE
│   │   ├── Cache/
│   │   │   ├── ProfileCacheService.cs            ← EXISTS (US_045)
│   │   │   ├── IConflictCacheService.cs          ← CREATE
│   │   │   └── ConflictCacheService.cs           ← CREATE
│   │   ├── DTOs/
│   │   │   ├── PatientProfileDto.cs              ← EXISTS (US_045)
│   │   │   ├── ConflictAlertDto.cs               ← CREATE
│   │   │   └── ConflictAlertsResponseDto.cs      ← CREATE
│   │   ├── Entities/
│   │   │   ├── ClinicalDocument.cs               ← EXISTS
│   │   │   ├── ClinicalFact.cs                   ← EXISTS (US_044)
│   │   │   ├── ConflictAlert.cs                  ← CREATE
│   │   │   └── ConflictRule.cs                   ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalFactRepository.cs        ← EXISTS
│   │   │   ├── IConflictAlertRepository.cs       ← CREATE
│   │   │   ├── ConflictAlertRepository.cs        ← CREATE
│   │   │   ├── IConflictRuleRepository.cs        ← CREATE
│   │   │   └── ConflictRuleRepository.cs         ← CREATE
│   │   └── [existing module structure...]
│   ├── SharedServices/
│   │   └── AuditService.cs                       ← REUSE (verify LogAsync exists)
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/ConflictController.cs` | GET conflicts (Clinician+Staff); POST acknowledge (Clinician only) |
| CREATE | `Modules/ClinicalIntelligence/Services/IConflictDetectionService.cs` | Detection interface |
| CREATE | `Modules/ClinicalIntelligence/Services/ConflictDetectionService.cs` | Cross-product rule evaluation, deduplication, staleness check, upsert alerts |
| CREATE | `Modules/ClinicalIntelligence/Cache/IConflictCacheService.cs` | Cache interface |
| CREATE | `Modules/ClinicalIntelligence/Cache/ConflictCacheService.cs` | Redis 30s TTL; invalidate on acknowledge |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ConflictAlertDto.cs` | Conflict response: conflictId, type, severity, description, drugA, drugB, acknowledged |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ConflictAlertsResponseDto.cs` | Response wrapper: alerts list + rulesStale flag |
| CREATE | `Modules/ClinicalIntelligence/Entities/ConflictAlert.cs` | EF entity: conflict_alerts table |
| CREATE | `Modules/ClinicalIntelligence/Entities/ConflictRule.cs` | EF entity: conflict_rules table |
| CREATE | `Modules/ClinicalIntelligence/Repositories/IConflictAlertRepository.cs` | GetByPatientIdAsync, AcknowledgeAsync |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ConflictAlertRepository.cs` | EF Core implementation |
| CREATE | `Modules/ClinicalIntelligence/Repositories/IConflictRuleRepository.cs` | GetActiveRulesAsync, GetLastUpdatedAtAsync |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ConflictRuleRepository.cs` | EF Core implementation with in-memory cache |

---

## External References

- EF Core upsert pattern: https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete#upsert
- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/dotnet/
- FR-CA-003: Detect drug-drug and drug-allergy conflicts, classify severity, require clinician acknowledgment of critical alerts
- NFR-010: Immutable audit evidence for access events, coding decisions, and overrides with 7-year retention
- TR-004: Redis caching for hot profile reads with bounded TTL
- SCR-016 access matrix: Clinician (Read/Write), Staff (Read only) — Staff cannot acknowledge

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

- [ ] `GET /api/v1/patients/{id}/conflicts` returns HTTP 200 with sorted conflicts (Critical first) (AC-1, AC-2)
- [ ] Drug-drug conflict detected: two medications matching a `drug_drug` rule appear in the response with correct severity
- [ ] Drug-allergy conflict detected: medication + allergy matching a `drug_allergy` rule appear in response
- [ ] Deduplication: same drug pair produces one entry with highest severity (Edge Case 2)
- [ ] `rulesStale: true` when `conflict_rules.last_updated_at` is older than staleness threshold (Edge Case 1)
- [ ] `POST /api/v1/conflicts/{id}/acknowledge` returns HTTP 204; `conflict_alerts` row updated with `acknowledged=true`, `acknowledged_by`, `acknowledged_at`
- [ ] Audit record written after acknowledgment with `event_type="conflict_acknowledged"`, clinician ID, and timestamp (AC-4, NFR-010)
- [ ] Staff role calling `POST acknowledge` returns HTTP 403
- [ ] Unauthenticated caller returns HTTP 401 on both endpoints
- [ ] Redis cache served on second GET call for same patient
- [ ] Cache invalidated after acknowledge — next GET reflects updated acknowledged state
- [ ] OpenTelemetry span and `conflict.detection.duration_ms` metric emitted per request

---

## Implementation Checklist

- [ ] Create `ConflictAlertDto` and `ConflictAlertsResponseDto` DTOs
- [ ] Create `ConflictAlert` and `ConflictRule` EF entities
- [ ] Create `IConflictAlertRepository` / `ConflictAlertRepository` with `GetByPatientIdAsync` and `AcknowledgeAsync`
- [ ] Create `IConflictRuleRepository` / `ConflictRuleRepository` with `GetActiveRulesAsync` and `GetLastUpdatedAtAsync`
- [ ] Create `IConflictDetectionService` / `ConflictDetectionService`: normalize names, cross-product evaluation, deduplication (Edge Case 2), staleness check (Edge Case 1), upsert alerts
- [ ] Create `IConflictCacheService` / `ConflictCacheService` with 30s Redis TTL and cache invalidation on acknowledge
- [ ] Create `ConflictController` with GET conflicts (`Clinician+Staff`) and POST acknowledge (`Clinician` only); integrate AuditService (AC-4, NFR-010)
- [ ] Add OpenTelemetry span + `conflict.detection.duration_ms` metric; register all services in DI

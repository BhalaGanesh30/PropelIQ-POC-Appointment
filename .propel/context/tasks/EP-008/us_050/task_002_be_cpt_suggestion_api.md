---
task_id: task_002
user_story: us_050
epic: EP-008
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_050] CPT and E/M Mapping Suggestions
- **Story Location**: [.propel/context/tasks/EP-008/us_050/us_050.md](.propel/context/tasks/EP-008/us_050/us_050.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient's clinical profile and appointment details are available, When I request CPT/E/M suggestions, Then the system returns ranked CPT codes and an E/M level suggestion with confidence scores and rationale within 2.5 seconds p95.
  - AC-2: Given CPT suggestions are displayed, When I view a suggestion card, Then the CPT code, description, confidence score, rationale text, and a link to supporting clinical evidence are all visible.
  - AC-3: Given an E/M level suggestion is provided, When I view the E/M mapping, Then the suggested E/M level is explained with the contributing clinical complexity factors.
  - AC-4: Given the AI model confidence for CPT is below threshold, When suggestions are returned, Then a "Manual coding recommended" indicator is shown with the low-confidence flag.
- **Edge Cases**:
  - Edge Case 1: Appointment type not mappable to CPT — return HTTP 200 with `{ noSuggestionForAppointmentType: true, cptSuggestions: [], emSuggestion: null }` (never 404 or 422 for this case).
  - Edge Case 2: Active CPT code database older than 90 days — include `staleDatabaseWarning: true` in response; deprecated codes excluded from all suggestions; endpoint continues to return suggestions with the warning flag.

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
| Database | PostgreSQL 15.x + pgvector | 15.x |
| Cache | Redis (StackExchange.Redis) | 2.x |
| Observability | OpenTelemetry | latest |
| Frontend | N/A | N/A |
| AI/ML | Azure OpenAI GPT-4.1 via LiteLLM gateway | 2026 APIs |
| AI Gateway | LiteLLM | latest stable |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-003, AIR-004, AIR-005, AIR-006, AIR-008, AIR-009, AIR-010 |
| **AI Pattern** | Hybrid (RAG retrieval + LLM inference + deterministic CPT validation against reference table) |
| **Prompt Template Path** | `.propel/context/prompts/cpt_em_suggestion_prompt.md` (to be created) |
| **Guardrails Config** | Schema validation ≥ 99% (AIR-008); confidence threshold check (AIR-005); PII redaction (AIR-009); deprecated CPT code exclusion via deterministic `cpt_codes` reference table filter |
| **Model Provider** | Azure OpenAI GPT-4.1 via LiteLLM gateway |

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

Implement `GET /api/v1/patients/{id}/coding-suggestions/cpt?appointmentId={appointmentId}` in the `ClinicalIntelligence` module. The endpoint generates CPT and E/M level suggestions via a Hybrid pipeline that combines LLM inference with deterministic CPT reference table validation.

Pipeline steps:
1. **Authorization** — `[Authorize(Roles = "Clinician")]`.
2. **Appointment type lookup** — Query `appointments` table by `appointmentId`; if `appointment_type` has no CPT mapping, return HTTP 200 with `noSuggestionForAppointmentType: true` (Edge Case 1).
3. **CPT database freshness check** — Query `cpt_codes.last_updated_at`; if `now() - last_updated_at > 90 days`, set `staleDatabaseWarning: true` in response (Edge Case 2). Deprecated codes (`is_deprecated = true`) are excluded from all suggestion candidates.
4. **ACL-filtered pgvector retrieval (AIR-010)** — Reuse `EvidenceRetrievalService` from US_049; retrieve top-K patient-scoped clinical facts as context.
5. **PII redaction (AIR-009)** — Reuse `PiiRedactionService` from US_049.
6. **CPT/E/M prompt assembly** — Assemble prompt with appointment type, clinical evidence, and active (non-deprecated) CPT code candidates; JSON output schema: `cpt_suggestions[]` (code, description, confidence, rationale, fact_ids), `em_suggestion` (em_level, description, confidence, rationale, complexity_factors[]).
7. **LLM inference** — GPT-4.1 via LiteLLM; Polly circuit-breaker; ≤ 2.5s p95 (AIR-006).
8. **Schema validation (AIR-008)** — Reuse `CodingSchemaValidator`; validate CPT + E/M output schema; retry once on failure.
9. **Post-LLM deterministic CPT validation** — Verify each suggested CPT code exists in `cpt_codes` with `is_deprecated = false`; remove any LLM-hallucinated or deprecated codes from the result set. This is the deterministic guardrail layer of the Hybrid pattern.
10. **Confidence threshold check (AIR-005)** — Min confidence across CPT suggestions triggers `lowConfidence: true` if below `IConfiguration["AI:CodingConfidenceThreshold"]` (default 0.75).
11. **Citation attachment (AIR-004)** — Resolve `fact_ids` from LLM response to `ClinicalFactCitationDto` records.
12. **Persist coding decisions** — Reuse `CodingDecisionRepository`; insert rows with `cpt_code` set (and `icd10_code` null); `reviewer_action = pending`.
13. **Return** `CptSuggestionResponseDto` with CPT suggestions, E/M suggestion, `lowConfidence`, `staleDatabaseWarning`, `noSuggestionForAppointmentType`.

Redis cache: 90-second TTL keyed by `patientId + appointmentId`; invalidated when patient's `clinical_facts` are updated.

---

## Dependent Tasks

- **us_050/task_003** — `cpt_codes` reference table must exist before the CPT freshness check and deterministic validation steps.
- **us_049/task_002** — `EvidenceRetrievalService`, `PiiRedactionService`, `CodingSchemaValidator`, `CodingDecisionRepository` must exist for reuse.
- **us_049/task_003** — `coding_decisions` table must exist (`cpt_code` column already present, nullable).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CptSuggestionController` | CREATE | `GET /api/v1/patients/{id}/coding-suggestions/cpt?appointmentId=`; Clinician-only |
| `ICptSuggestionOrchestrator` | CREATE | Interface: `GenerateCptSuggestionsAsync(Guid patientId, Guid appointmentId, CancellationToken ct)` |
| `CptSuggestionOrchestrator` | CREATE | Coordinates: appointment lookup → freshness check → retrieval → PII redact → LLM → schema validate → CPT validate → confidence check → citations → persist |
| `ICptCodeFreshnessService` | CREATE | Interface: `CheckFreshnessAsync(CancellationToken ct): Task<CptFreshnessResult>` — returns `IsStale`, `LastUpdatedAt` |
| `CptCodeFreshnessService` | CREATE | Queries `MAX(last_updated_at)` from `cpt_codes`; compares against 90-day threshold from `IConfiguration["CPT:MaxAgeDays"]` (default 90) |
| `ICptCodeValidationService` | CREATE | Interface: validates LLM-suggested CPT codes against active `cpt_codes` entries |
| `CptCodeValidationService` | CREATE | Filters out codes where `is_deprecated = true` or code not found in `cpt_codes`; deterministic guardrail |
| `ICptCodeRepository` | CREATE | Interface: `GetActiveCodesAsync(CancellationToken ct)`, `GetLastUpdatedAtAsync(CancellationToken ct)`, `ExistsAndActiveAsync(string cptCode, CancellationToken ct)` |
| `CptCodeRepository` | CREATE | EF Core queries on `cpt_codes` table |
| `IAppointmentTypeMapper` | CREATE | Interface: `IsMappableToCpt(string appointmentType): bool` — deterministic lookup |
| `AppointmentTypeMapper` | CREATE | Configuration-driven mapping of appointment types to CPT candidacy; reads from `IConfiguration["CPT:MappableAppointmentTypes"]` |
| `CptSuggestionDto` | CREATE | Per-suggestion DTO: `decisionId`, `cptCode`, `description`, `confidence`, `rationale`, `citations` |
| `EmSuggestionDto` | CREATE | E/M DTO: `decisionId`, `emLevel`, `description`, `confidence`, `rationale`, `complexityFactors: string[]` |
| `CptSuggestionResponseDto` | CREATE | Response wrapper: `cptSuggestions`, `emSuggestion`, `lowConfidence`, `staleDatabaseWarning`, `noSuggestionForAppointmentType` |
| `EvidenceRetrievalService` | REUSE | US_049 — no modification |
| `PiiRedactionService` | REUSE | US_049 — no modification |
| `CodingSchemaValidator` | REUSE | US_049 — extend schema definition for CPT+E/M output shape |
| `CodingDecisionRepository` | REUSE | US_049 — `InsertPendingAsync` with `cpt_code` set, `icd10_code` null |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new services and repositories |

---

## Implementation Plan

1. **Create DTOs**: `CptSuggestionDto` (`Guid DecisionId`, `string CptCode`, `string Description`, `decimal Confidence`, `string Rationale`, `List<ClinicalFactCitationDto> Citations`); `EmSuggestionDto` (`Guid DecisionId`, `string EmLevel`, `string Description`, `decimal Confidence`, `string Rationale`, `List<string> ComplexityFactors`); `CptSuggestionResponseDto` (`List<CptSuggestionDto> CptSuggestions`, `EmSuggestionDto? EmSuggestion`, `bool LowConfidence`, `bool StaleDatabaseWarning`, `bool NoSuggestionForAppointmentType`). Internal `LlmCptResponse` record for GPT-4.1 deserialization.
2. **Create `IAppointmentTypeMapper` / `AppointmentTypeMapper`**: Deterministic lookup from `IConfiguration["CPT:MappableAppointmentTypes"]` (comma-separated list, e.g., "office_visit,follow_up,new_patient"). `IsMappableToCpt(string appointmentType)` returns `true` if type is in the configured list. This allows operational config without code changes.
3. **Create `ICptCodeRepository` / `CptCodeRepository`**: `GetLastUpdatedAtAsync` → `SELECT MAX(last_updated_at) FROM cpt_codes`. `ExistsAndActiveAsync(string cptCode)` → `AnyAsync(c => c.CptCode == cptCode && !c.IsDeprecated)`.
4. **Create `ICptCodeFreshnessService` / `CptCodeFreshnessService`**: Calls `CptCodeRepository.GetLastUpdatedAtAsync()`; computes `now() - lastUpdatedAt`; if `> TimeSpan.FromDays(config["CPT:MaxAgeDays"])` → `IsStale = true`. Cache the freshness result in Redis with 1-hour TTL to avoid repeated DB queries per request.
5. **Create `ICptCodeValidationService` / `CptCodeValidationService`**: For each code in LLM output, call `CptCodeRepository.ExistsAndActiveAsync(cptCode)`. Reject codes that do not exist or are deprecated. If all codes rejected, the orchestrator falls back to `NoSuggestionForAppointmentType = false` (suggestions exhausted) and sets `LowConfidence = true`.
6. **Create `ICptSuggestionOrchestrator` / `CptSuggestionOrchestrator`**: Coordinates the full pipeline (steps 2–13 in Task Overview). Appointment type check first — short-circuit return if not mappable. CPT freshness check second — set flag, continue. Reuse `EvidenceRetrievalService` + `PiiRedactionService` from US_049. Call `CodingAiGatewayClient` with CPT-specific prompt template. Validate schema (reuse `CodingSchemaValidator` with extended CPT schema). Run deterministic CPT validation. Check confidence threshold. Attach citations. Persist pending decisions via `CodingDecisionRepository`.
7. **Create `CptSuggestionController`**: `[HttpGet("patients/{id}/coding-suggestions/cpt")]`, `[Authorize(Roles = "Clinician")]`. Bind `id` and `appointmentId` query param. Validate both as Guid. Check Redis cache → on miss call orchestrator → cache result (90s TTL) → return HTTP 200 `CptSuggestionResponseDto` (always 200 — edge cases communicated via response flags, not HTTP error codes).
8. **Extend `CodingSchemaValidator` for CPT output shape**: Add a CPT-specific schema definition: requires `cpt_suggestions[]` with `cpt_code`, `description`, `confidence`, `rationale`, `fact_ids`; requires `em_suggestion` with `em_level`, `description`, `confidence`, `rationale`, `complexity_factors[]`. Emit `coding.cpt_schema_validation_pass` / `coding.cpt_schema_validation_fail` metrics (AIR-008).

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── CodingSuggestionController.cs         ← EXISTS (US_049)
│   │   │   └── CptSuggestionController.cs            ← CREATE
│   │   ├── Services/
│   │   │   ├── CodingSuggestionOrchestrator.cs       ← EXISTS (US_049)
│   │   │   ├── EvidenceRetrievalService.cs           ← REUSE (US_049)
│   │   │   ├── PiiRedactionService.cs                ← REUSE (US_049)
│   │   │   ├── ICptSuggestionOrchestrator.cs         ← CREATE
│   │   │   ├── CptSuggestionOrchestrator.cs          ← CREATE
│   │   │   ├── ICptCodeFreshnessService.cs           ← CREATE
│   │   │   ├── CptCodeFreshnessService.cs            ← CREATE
│   │   │   ├── ICptCodeValidationService.cs          ← CREATE
│   │   │   ├── AppointmentTypeMapper.cs              ← CREATE
│   │   │   └── CptCodeValidationService.cs           ← CREATE
│   │   ├── AI/
│   │   │   ├── CodingAiGatewayClient.cs              ← REUSE (US_049)
│   │   │   └── CodingSchemaValidator.cs              ← MODIFY (add CPT schema)
│   │   ├── Repositories/
│   │   │   ├── CodingDecisionRepository.cs           ← REUSE (US_049)
│   │   │   ├── ICptCodeRepository.cs                 ← CREATE
│   │   │   └── CptCodeRepository.cs                  ← CREATE
│   │   └── DTOs/
│   │       ├── CptSuggestionDto.cs                   ← CREATE
│   │       ├── EmSuggestionDto.cs                    ← CREATE
│   │       └── CptSuggestionResponseDto.cs           ← CREATE
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/CptSuggestionController.cs` | GET .../coding-suggestions/cpt; always HTTP 200; edge cases via response flags |
| CREATE | `Modules/ClinicalIntelligence/Services/ICptSuggestionOrchestrator.cs` | Orchestrator interface |
| CREATE | `Modules/ClinicalIntelligence/Services/CptSuggestionOrchestrator.cs` | Full Hybrid pipeline: appointment check → freshness → RAG → LLM → deterministic CPT validation → persist |
| CREATE | `Modules/ClinicalIntelligence/Services/ICptCodeFreshnessService.cs` | CPT DB freshness interface |
| CREATE | `Modules/ClinicalIntelligence/Services/CptCodeFreshnessService.cs` | 90-day threshold check; Redis 1h TTL for freshness result |
| CREATE | `Modules/ClinicalIntelligence/Services/ICptCodeValidationService.cs` | Deterministic CPT validation interface |
| CREATE | `Modules/ClinicalIntelligence/Services/CptCodeValidationService.cs` | Reject deprecated/non-existent codes from LLM output |
| CREATE | `Modules/ClinicalIntelligence/Services/IAppointmentTypeMapper.cs` | Interface for appointment-type-to-CPT-candidacy check |
| CREATE | `Modules/ClinicalIntelligence/Services/AppointmentTypeMapper.cs` | Configuration-driven; IsMappableToCpt |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ICptCodeRepository.cs` | GetLastUpdatedAtAsync, ExistsAndActiveAsync |
| CREATE | `Modules/ClinicalIntelligence/Repositories/CptCodeRepository.cs` | EF Core queries on cpt_codes |
| CREATE | `Modules/ClinicalIntelligence/DTOs/CptSuggestionDto.cs` | Per-suggestion CPT DTO |
| CREATE | `Modules/ClinicalIntelligence/DTOs/EmSuggestionDto.cs` | E/M level DTO with complexityFactors list (AC-3) |
| CREATE | `Modules/ClinicalIntelligence/DTOs/CptSuggestionResponseDto.cs` | Response wrapper with all flags |
| MODIFY | `Modules/ClinicalIntelligence/AI/CodingSchemaValidator.cs` | Add CPT+E/M output schema definition; emit cpt_schema_validation metrics |

---

## External References

- Azure OpenAI structured outputs: https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/structured-outputs
- LiteLLM gateway: https://docs.litellm.ai/docs/
- Polly circuit-breaker: https://www.pollydocs.org/strategies/circuit-breaker.html
- AIR-003: Top-3 CPT suggestions with explicit rationale linked to clinical evidence
- AIR-004: Citation references for coding rationale (fact_ids → ClinicalFactCitationDto)
- AIR-005: Fallback / lowConfidence flag when model confidence below threshold (AC-4)
- AIR-006: AI response latency ≤ 2.5 seconds p95 (AC-1)
- AIR-008: Output schema validation ≥ 99%; cpt_schema_validation_pass metric
- AIR-009: PII redaction from prompts; reuse PiiRedactionService from US_049
- AIR-010: ACL-filtered retrieval; reuse EvidenceRetrievalService patient-scoped filter
- FR-MC-002 [HYBRID]: CPT and E/M mapping suggestions with explainable rationale

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

- [ ] `GET /api/v1/patients/{id}/coding-suggestions/cpt?appointmentId=` returns HTTP 200 with CPT suggestions and E/M suggestion within 2.5s p95 (AC-1, AIR-006)
- [ ] Each `CptSuggestionDto` contains `cptCode`, `description`, `confidence`, `rationale`, and `citations` (AC-2, AIR-004)
- [ ] `EmSuggestionDto` contains `emLevel`, `description`, `confidence`, `rationale`, and `complexityFactors[]` (AC-3)
- [ ] LLM-suggested deprecated CPT codes are excluded by `CptCodeValidationService` before response is returned (Edge Case 2)
- [ ] When CPT DB `last_updated_at` > 90 days ago → `staleDatabaseWarning: true` in response (Edge Case 2)
- [ ] Unmappable appointment type → HTTP 200 with `noSuggestionForAppointmentType: true` (Edge Case 1)
- [ ] Min CPT suggestion confidence < threshold → `lowConfidence: true` in response (AC-4, AIR-005)
- [ ] CPT schema validation failure triggers retry; `coding.cpt_schema_validation_fail` metric emitted (AIR-008)
- [ ] PII redaction applied before prompt assembly; redaction logged to AuditService (AIR-009)
- [ ] pgvector query uses `patient_id = :patientId` ACL filter (AIR-010)
- [ ] Redis cache (90s TTL) serves second identical request; `coding.suggestion.duration_ms` histogram emitted

---

## Implementation Checklist

- [ ] Create `CptSuggestionDto`, `EmSuggestionDto`, `CptSuggestionResponseDto`, `LlmCptResponse` DTOs
- [ ] Create `IAppointmentTypeMapper` / `AppointmentTypeMapper`: configuration-driven CPT candidacy check (Edge Case 1)
- [ ] Create `ICptCodeRepository` / `CptCodeRepository`: `GetLastUpdatedAtAsync`, `ExistsAndActiveAsync` (Edge Case 2)
- [ ] Create `ICptCodeFreshnessService` / `CptCodeFreshnessService`: 90-day threshold check with Redis 1h TTL for freshness result (Edge Case 2)
- [ ] Create `ICptCodeValidationService` / `CptCodeValidationService`: deterministic post-LLM deprecated/non-existent code rejection
- [ ] Create `ICptSuggestionOrchestrator` / `CptSuggestionOrchestrator`: full Hybrid pipeline; reuse `EvidenceRetrievalService`, `PiiRedactionService`, `CodingDecisionRepository` from US_049; confidence threshold → `lowConfidence` flag (AC-4)
- [ ] Modify `CodingSchemaValidator` to add CPT+E/M output schema; emit `coding.cpt_schema_validation_pass/fail` metrics (AIR-008)
- [ ] Create `CptSuggestionController`: always HTTP 200; Redis 90s TTL; register all new services in DI; OpenTelemetry span + `coding.suggestion.duration_ms` metric

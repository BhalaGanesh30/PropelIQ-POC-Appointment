---
task_id: task_002
user_story: us_049
epic: EP-008
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_049] ICD-10 Code Suggestion Generation
- **Story Location**: [.propel/context/tasks/EP-008/us_049/us_049.md](.propel/context/tasks/EP-008/us_049/us_049.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient's clinical profile is available, When I request ICD-10 suggestions, Then the system returns the top 3 ranked ICD-10 codes with confidence scores and a rationale text linked to specific extracted clinical facts within 2.5 seconds p95.
  - AC-2: Given suggestions are returned, When I view the suggestion panel, Then each suggestion displays the ICD-10 code, description, confidence score, and a "View Evidence" link that opens the supporting clinical facts.
  - AC-3: Given the AI model confidence is below the configured threshold, When suggestions are generated, Then the system flags the result as low-confidence and prominently displays "Manual review recommended" before presenting the suggestions.
  - AC-4: Given the suggestion API is called, When the output schema is validated, Then at least 99% of responses pass schema validation with all required fields present.
- **Edge Cases**:
  - Edge Case 1: Fewer than 3 valid ICD-10 codes generated — return available codes (1 or 2); set `insufficientEvidence: true` in response; never pad with placeholder codes.
  - Edge Case 2: No extracted clinical facts for the patient — return HTTP 422 with body `{ "error": "Insufficient clinical data for code suggestion. Please review the patient's clinical profile." }`.

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
| **AI Pattern** | Hybrid (RAG retrieval + LLM inference + deterministic schema validation) |
| **Prompt Template Path** | `.propel/context/prompts/icd10_suggestion_prompt.md` (to be created) |
| **Guardrails Config** | Schema validation ≥ 99% (AIR-008); confidence threshold check (AIR-005); PII redaction before prompt assembly (AIR-009) |
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

Implement `GET /api/v1/patients/{id}/coding-suggestions` in the `ClinicalIntelligence` module following the UC-005 AI Subflow (Retrieval-Backed Coding Suggestions). The pipeline:

1. **Authorization check** — Clinician role only (`[Authorize(Roles = "Clinician")]`).
2. **Pre-flight validation** — Query `clinical_facts` for the patient; if none exist, return HTTP 422 (Edge Case 2).
3. **ACL-filtered pgvector retrieval (AIR-010)** — Embed the patient's `fact_type` + `name` concatenation via `text-embedding-3-small`; query `clinical_facts.embedding` HNSW index with a patient-scoped filter (`fact.patient_id = :id`) to retrieve top-K ranked evidence chunks.
4. **PII redaction (AIR-009)** — Strip direct identifiers from context before prompt assembly; log redaction actions.
5. **Prompt assembly** — Assemble ICD-10 coding prompt with retrieved evidence chunks as context; use structured output JSON schema requiring: `icd10_code`, `description`, `confidence` (0.0–1.0), `rationale`, `fact_ids` (citation list).
6. **LLM inference** — Call Azure OpenAI GPT-4.1 via LiteLLM; Polly circuit-breaker for resilience.
7. **Schema validation (AIR-008)** — Validate response against required schema; reject and retry once on schema failure; emit `coding.schema_validation_pass` and `coding.schema_validation_fail` metrics.
8. **Confidence threshold check (AIR-005)** — If any suggestion has `confidence < configuredThreshold` (default 0.75), set `lowConfidence: true` on response.
9. **Citation attachment (AIR-004)** — Resolve `fact_ids` from LLM response back to full `ClinicalFactCitationDto` records from DB.
10. **Persist pending coding decisions** — Insert one `coding_decisions` row per suggestion with `reviewer_action = "pending"`.
11. **Return** top-3 `IcdSuggestionDto` list (or fewer if Edge Case 1), `lowConfidence`, `insufficientEvidence`.

Redis cache: 90-second TTL keyed by `patientId`; invalidated when `clinical_facts` are updated for the patient. OpenTelemetry span per request with `coding.suggestion.duration_ms` metric.

---

## Dependent Tasks

- **us_049/task_003** — `coding_decisions` table must exist before `persist pending coding decisions` step.
- **us_044/task_001** — `clinical_facts` with `embedding vector(1536)` must exist; HNSW index required for pgvector retrieval.
- **us_044/task_002** — `clinical_facts` table schema must include `fact_date`, `fact_type`, `name`, `value`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CodingSuggestionController` | CREATE | `GET /api/v1/patients/{id}/coding-suggestions`; Clinician-only; delegates to `ICodingSuggestionOrchestrator` |
| `ICodingSuggestionOrchestrator` | CREATE | Interface: `GenerateSuggestionsAsync(Guid patientId, CancellationToken ct)` |
| `CodingSuggestionOrchestrator` | CREATE | Coordinates: preflight → ACL retrieval → PII redaction → prompt assembly → LLM → schema validation → confidence check → citation attach → persist |
| `IEvidenceRetrievalService` | CREATE | Interface: ACL-filtered pgvector top-K retrieval with patient-scoped embedding query (AIR-010) |
| `EvidenceRetrievalService` | CREATE | Embeds query text via `text-embedding-3-small`; runs HNSW query with `fact.patient_id = patientId` filter |
| `IPiiRedactionService` | CREATE | Interface: `RedactAsync(string text): Task<string>`; logs redaction actions (AIR-009) |
| `PiiRedactionService` | CREATE | Strips direct identifiers (name, DOB, SSN patterns) from assembled context; audit-logs each redaction |
| `ICodingAiGatewayClient` | CREATE | Interface: `RequestSuggestionsAsync(string prompt, CancellationToken ct): Task<LlmCodingResponse>` |
| `CodingAiGatewayClient` | CREATE | Calls LiteLLM GPT-4.1 with structured JSON output schema; Polly circuit-breaker (AIR-006) |
| `ICodingSchemaValidator` | CREATE | Interface: validates `LlmCodingResponse` against required field schema; returns validation result |
| `CodingSchemaValidator` | CREATE | System.Text.Json schema check; emits `coding.schema_validation_pass` / `coding.schema_validation_fail` metrics (AIR-008) |
| `ICodingDecisionRepository` | CREATE | Interface: `InsertPendingAsync(IEnumerable<CodingDecisionEntity> decisions, CancellationToken ct)` |
| `CodingDecisionRepository` | CREATE | EF Core bulk insert to `coding_decisions` with `reviewer_action = pending` |
| `IcdSuggestionDto` | CREATE | Response DTO per suggestion: `decisionId`, `icdCode`, `description`, `confidence`, `rationale`, `citations` |
| `CodingSuggestionResponseDto` | CREATE | Wraps `List<IcdSuggestionDto>`, `bool LowConfidence`, `bool InsufficientEvidence` |
| `IClinicalFactRepository` | MODIFY | Add `HasFactsAsync(Guid patientId, CancellationToken ct)` for HTTP 422 preflight check |
| `ClinicalIntelligenceModule` DI | MODIFY | Register all new services and repositories |

---

## Implementation Plan

1. **Create DTOs**: `IcdSuggestionDto` (`Guid DecisionId`, `string IcdCode`, `string Description`, `decimal Confidence`, `string Rationale`, `List<ClinicalFactCitationDto> Citations`); `ClinicalFactCitationDto` (`Guid FactId`, `string FactType`, `string Name`, `string Value`, `DateTimeOffset FactDate`); `CodingSuggestionResponseDto` (`List<IcdSuggestionDto> Suggestions`, `bool LowConfidence`, `bool InsufficientEvidence`); `LlmCodingResponse` (internal, deserialized from GPT-4.1 output).
2. **Extend `IClinicalFactRepository`**: Add `HasFactsAsync(Guid patientId, CancellationToken ct): Task<bool>`. Implement with `AnyAsync(f => f.PatientId == patientId)` — used for HTTP 422 preflight.
3. **Create `IEvidenceRetrievalService` / `EvidenceRetrievalService`**: Embed the patient's fact set summary (`string.Join("; ", factNames)`) via `text-embedding-3-small`. Query `clinical_facts` HNSW index with `embedding <=> queryVector ORDER BY distance LIMIT 10` where `patient_id = patientId` (ACL filter — AIR-010). Return `List<ClinicalFactCitationDto>` ranked by cosine distance.
4. **Create `IPiiRedactionService` / `PiiRedactionService`**: Apply regex-based redaction for direct identifiers (full name, SSN, DOB) before fact content is inserted into the prompt. Log each redaction action to `AuditService` with `event_type = "pii_redaction"` (AIR-009).
5. **Create `ICodingAiGatewayClient` / `CodingAiGatewayClient`**: Assemble structured prompt with evidence chunks as system context and JSON output schema specifying required fields (`icd10_code`, `description`, `confidence`, `rationale`, `fact_ids[]`). Call LiteLLM endpoint. Polly circuit-breaker: 5 consecutive failures → open circuit for 30s. Target ≤ 2.5s p95 (AIR-006).
6. **Create `ICodingSchemaValidator` / `CodingSchemaValidator`**: Deserialize LLM JSON output into `LlmCodingResponse`. Validate all required fields are present and non-null; `confidence` is 0.0–1.0. On failure, log `coding.schema_validation_fail`, retry once. If retry fails, trigger manual fallback response. Emit `coding.schema_validation_pass` on success (AIR-008).
7. **Create `ICodingDecisionRepository` / `CodingDecisionRepository`**: `InsertPendingAsync` bulk-inserts one `CodingDecisionEntity` per suggestion: `patient_id`, `fact_id` (primary citation), `icd10_code`, `confidence`, `rationale`, `reviewer_action = "pending"`. Returns list of generated `decision_id` GUIDs.
8. **Create `ICodingSuggestionOrchestrator` / `CodingSuggestionOrchestrator`**: Coordinates the full pipeline (steps 2–7 in Task Overview). Confidence threshold: read from `IConfiguration["AI:CodingConfidenceThreshold"]` (default `0.75`). After schema validation, check min confidence across all suggestions — if any below threshold, set `LowConfidence = true` (AC-3). Attach citation DTOs by resolving `fact_ids` from LLM response against the retrieved evidence set. Limit to top 3 suggestions sorted by `confidence DESC`; if fewer than 3 valid, set `InsufficientEvidence = true` (Edge Case 1).
9. **Create `CodingSuggestionController`**: `[HttpGet("patients/{id}/coding-suggestions")]`, `[Authorize(Roles = "Clinician")]`. Validate `id` as Guid. Check cache → on miss call orchestrator → cache result (90s TTL) → return `HTTP 200 OK` with `CodingSuggestionResponseDto`. HTTP 422 returned when orchestrator throws `InsufficientClinicalDataException` (Edge Case 2).
10. **OpenTelemetry instrumentation**: Activity span wrapping orchestrator; tags: `patient.id`, `suggestions.count`, `low_confidence`; counter `coding.schema_validation_pass` / `coding.schema_validation_fail`; histogram `coding.suggestion.duration_ms`. Register in DI.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── PatientProfileController.cs           ← EXISTS (US_045)
│   │   │   ├── ConflictController.cs                 ← EXISTS (US_046)
│   │   │   ├── ClinicalFactsController.cs            ← EXISTS (US_047)
│   │   │   ├── TimelineController.cs                 ← EXISTS (US_048)
│   │   │   └── CodingSuggestionController.cs         ← CREATE
│   │   ├── Services/
│   │   │   ├── ICodingSuggestionOrchestrator.cs      ← CREATE
│   │   │   ├── CodingSuggestionOrchestrator.cs       ← CREATE
│   │   │   ├── IEvidenceRetrievalService.cs          ← CREATE
│   │   │   ├── EvidenceRetrievalService.cs           ← CREATE
│   │   │   ├── IPiiRedactionService.cs               ← CREATE
│   │   │   └── PiiRedactionService.cs                ← CREATE
│   │   ├── AI/
│   │   │   ├── ICodingAiGatewayClient.cs             ← CREATE
│   │   │   ├── CodingAiGatewayClient.cs              ← CREATE
│   │   │   ├── ICodingSchemaValidator.cs             ← CREATE
│   │   │   └── CodingSchemaValidator.cs              ← CREATE
│   │   ├── Cache/
│   │   │   └── [existing cache services...]
│   │   ├── DTOs/
│   │   │   ├── IcdSuggestionDto.cs                   ← CREATE
│   │   │   ├── ClinicalFactCitationDto.cs            ← CREATE
│   │   │   └── CodingSuggestionResponseDto.cs        ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalFactRepository.cs            ← MODIFY (add HasFactsAsync)
│   │   │   ├── ClinicalFactRepository.cs             ← MODIFY (implement HasFactsAsync)
│   │   │   ├── ICodingDecisionRepository.cs          ← CREATE
│   │   │   └── CodingDecisionRepository.cs           ← CREATE
│   │   └── [existing module structure...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/CodingSuggestionController.cs` | GET /api/v1/patients/{id}/coding-suggestions; Clinician-only; HTTP 422 on InsufficientClinicalDataException |
| CREATE | `Modules/ClinicalIntelligence/Services/ICodingSuggestionOrchestrator.cs` | Orchestrator interface |
| CREATE | `Modules/ClinicalIntelligence/Services/CodingSuggestionOrchestrator.cs` | Full RAG pipeline: preflight → retrieval → PII redact → LLM → validate → persist |
| CREATE | `Modules/ClinicalIntelligence/Services/IEvidenceRetrievalService.cs` | ACL-filtered pgvector retrieval interface (AIR-010) |
| CREATE | `Modules/ClinicalIntelligence/Services/EvidenceRetrievalService.cs` | HNSW query with patient_id ACL filter; returns ranked evidence chunks |
| CREATE | `Modules/ClinicalIntelligence/Services/IPiiRedactionService.cs` | PII redaction interface (AIR-009) |
| CREATE | `Modules/ClinicalIntelligence/Services/PiiRedactionService.cs` | Regex-based identifier redaction; audit-logs each redaction |
| CREATE | `Modules/ClinicalIntelligence/AI/ICodingAiGatewayClient.cs` | LLM client interface with structured JSON schema |
| CREATE | `Modules/ClinicalIntelligence/AI/CodingAiGatewayClient.cs` | GPT-4.1 via LiteLLM; Polly circuit-breaker (AIR-006) |
| CREATE | `Modules/ClinicalIntelligence/AI/ICodingSchemaValidator.cs` | Schema validator interface |
| CREATE | `Modules/ClinicalIntelligence/AI/CodingSchemaValidator.cs` | Required field validation; retry once on fail; metrics (AIR-008) |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ICodingDecisionRepository.cs` | InsertPendingAsync interface |
| CREATE | `Modules/ClinicalIntelligence/Repositories/CodingDecisionRepository.cs` | EF Core bulk insert to coding_decisions |
| CREATE | `Modules/ClinicalIntelligence/DTOs/IcdSuggestionDto.cs` | Response DTO per suggestion |
| CREATE | `Modules/ClinicalIntelligence/DTOs/CodingSuggestionResponseDto.cs` | Response wrapper with lowConfidence + insufficientEvidence |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalFactRepository.cs` | Add HasFactsAsync for HTTP 422 preflight |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalFactRepository.cs` | Implement HasFactsAsync |

---

## External References

- Azure OpenAI structured outputs: https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/structured-outputs
- LiteLLM gateway: https://docs.litellm.ai/docs/
- pgvector HNSW indexing: https://github.com/pgvector/pgvector#indexing
- Polly circuit-breaker: https://www.pollydocs.org/strategies/circuit-breaker.html
- AIR-003: Top-3 ICD-10 suggestions with explicit rationale linked to clinical evidence
- AIR-004: Source citation references for coding rationale (fact_ids → ClinicalFactCitationDto)
- AIR-005: Fallback to manual coding when confidence below configured threshold
- AIR-006: AI response latency within 2.5 seconds p95 for synchronous suggestion APIs
- AIR-008: Output schema validation ≥ 99% (coding.schema_validation_pass metric)
- AIR-009: PII redaction from prompts; log redaction actions
- AIR-010: Retrieval access control — only patient-scoped context in AI reasoning
- FR-MC-001 [HYBRID]: Top-3 ICD-10 with confidence and explainable rationale
- UC-005 AI Subflow: Retrieval-Backed Coding Suggestions sequence diagram

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

- [ ] `GET /api/v1/patients/{id}/coding-suggestions` returns HTTP 200 with top-3 `IcdSuggestionDto` list sorted by `confidence DESC` within 2.5s p95 (AC-1, AIR-006)
- [ ] Each `IcdSuggestionDto` contains `icdCode`, `description`, `confidence`, `rationale`, and `citations` (AC-2, AIR-003, AIR-004)
- [ ] When min suggestion confidence < configured threshold → `lowConfidence: true` in response (AC-3, AIR-005)
- [ ] LLM output missing required fields triggers retry; second failure returns fallback response; `coding.schema_validation_fail` metric emitted (AC-4, AIR-008)
- [ ] pgvector query uses `patient_id = :patientId` ACL filter; no cross-patient context leakage (AIR-010)
- [ ] PII redaction applied before prompt assembly; redaction actions logged to `AuditService` (AIR-009)
- [ ] Patient with zero `clinical_facts` returns HTTP 422 with prescribed error message (Edge Case 2)
- [ ] Response with < 3 valid codes sets `insufficientEvidence: true`; no placeholder codes inserted (Edge Case 1)
- [ ] Redis cache serves second identical request within TTL; cache invalidated on fact update
- [ ] `coding.suggestion.duration_ms` OpenTelemetry histogram emitted per request

---

## Implementation Checklist

- [ ] Create `IcdSuggestionDto`, `ClinicalFactCitationDto`, `CodingSuggestionResponseDto`, `LlmCodingResponse` DTOs
- [ ] Extend `IClinicalFactRepository` / `ClinicalFactRepository` with `HasFactsAsync` for HTTP 422 preflight (Edge Case 2)
- [ ] Create `IEvidenceRetrievalService` / `EvidenceRetrievalService`: embed query, HNSW ACL-filtered retrieval, return ranked evidence (AIR-010)
- [ ] Create `IPiiRedactionService` / `PiiRedactionService`: regex redaction + audit log (AIR-009)
- [ ] Create `ICodingAiGatewayClient` / `CodingAiGatewayClient`: GPT-4.1 structured output, Polly circuit-breaker ≤ 2.5s p95 (AIR-006)
- [ ] Create `ICodingSchemaValidator` / `CodingSchemaValidator`: required field check, single retry, `coding.schema_validation_pass/fail` metrics (AIR-008)
- [ ] Create `ICodingDecisionRepository` / `CodingDecisionRepository`: bulk insert `reviewer_action = pending` rows (task_003 dependency)
- [ ] Create `ICodingSuggestionOrchestrator` / `CodingSuggestionOrchestrator`: full pipeline coordination, confidence threshold check, citation attachment, InsufficientEvidence flag (Edge Case 1), `lowConfidence` flag (AC-3)
- [ ] Create `CodingSuggestionController`: Clinician-only, Redis 90s TTL, HTTP 422 on `InsufficientClinicalDataException`; OpenTelemetry span + `coding.suggestion.duration_ms` metric

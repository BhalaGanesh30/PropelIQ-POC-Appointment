---
task_id: task_001
user_story: us_054
epic: EP-009
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_054] PII Redaction for AI Prompts
- **Story Location**: [.propel/context/tasks/EP-009/us_054/us_054.md](.propel/context/tasks/EP-009/us_054/us_054.md)
- **Acceptance Criteria**:
  - AC-1: Given an AI request is prepared, When the prompt is constructed, Then the PII redaction pipeline removes direct identifiers (name, date of birth, SSN, address) and replaces them with anonymized tokens before the prompt is sent to the model.
  - AC-2: Given a redaction action is performed, When redaction completes, Then the redaction event is logged with the fields redacted, the anonymization tokens used, and the request correlation ID.
  - AC-3: Given the redacted prompt is sent to the model, When the response is returned, Then de-anonymization mapping is applied to restore context references in the response without re-exposing raw PII in logs.
  - AC-4: Given a retrieval access control filter is applied, When context documents are assembled for the AI prompt, Then only patient-specific documents authorized for the requesting clinician are included; cross-patient context is never mixed.
- **Edge Cases**:
  - Edge Case 1: Redaction pipeline failure — the AI request is blocked; HTTP error returned to caller; failure logged with request correlation ID; no raw PII reaches the model.
  - Edge Case 2: PII in free-text clinical notes — NLP-based entity recognition (regex patterns + configurable confidence threshold) identifies PII patterns in free text; applies token substitution; confidence threshold governs detection sensitivity.

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
| AI/ML | Azure OpenAI GPT-4.1 via LiteLLM gateway | 2026 APIs |
| AI Gateway | LiteLLM + shared `IAiGatewayClient` (US_053) | latest stable |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-009, AIR-010, AIR-011 |
| **AI Pattern** | Guardrails (PII redaction + ACL enforcement pre-prompt; de-anonymization post-response) |
| **Prompt Template Path** | N/A — this task implements the redaction pipeline, not prompt content |
| **Guardrails Config** | `IConfiguration["AI:Redaction:ConfidenceThreshold"]` (default 0.85) for NLP pattern detection; configurable direct-identifier field list |
| **Model Provider** | Azure OpenAI GPT-4.1 (called via `IAiGatewayClient` from US_053) |

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

Implement the full PII redaction and ACL enforcement pipeline in the `SharedServices` module. This supersedes the stub `PiiRedactionService` scaffolded in US_049; all AI-calling services route through this shared implementation.

The pipeline executes in two phases:

**Phase 1 — Pre-prompt (redact):**
1. **Structured field redaction**: Scan `AiGatewayRequest.Prompt` for named structured fields (`patient_name`, `date_of_birth`, `ssn`, `address`) using a configurable field mapping. Replace each value with a deterministic token: `[REDACTED_NAME_abc123]`, `[REDACTED_DOB_xyz789]` etc. Token is derived from `HMAC-SHA256(fieldValue, tenantRedactionKey)` truncated to 8 chars — deterministic per request so de-anonymization can reverse it (AC-3).
2. **Free-text NLP redaction**: Apply regex-based entity recognition patterns for names (capitalized word pairs), dates (`\d{1,2}[/-]\d{1,2}[/-]\d{2,4}`), SSN (`\d{3}-\d{2}-\d{4}`), addresses (street number + word patterns), phone numbers. Patterns with detection confidence above `IConfiguration["AI:Redaction:ConfidenceThreshold"]` are substituted with tokens (Edge Case 2). Log patterns that were below confidence threshold as `pii_detection_low_confidence` but do not substitute.
3. **ACL context filter**: Before assembling the retrieval context (pgvector result set), apply `patient_id = :requestingPatientId` AND `authorized_clinician_id IN (:clinicianId)` filter. Cross-patient context is structurally impossible — reject any context chunk whose `patient_id != requestingPatientId` with an `ACLViolationException` that blocks the AI call (AC-4).
4. **Redaction map persistence**: Store `{ correlationId → Map<token, originalValue> }` in Redis with a 5-minute TTL (sufficient for the synchronous round-trip; TTL prevents long-lived PII in cache). Serialize with AES-256-GCM encryption using a tenant-scoped key from configuration.
5. **Redaction audit log**: Write `AuditService` entry `event_type = "pii_redacted"` with `{ correlation_id, fields_redacted: string[], token_count: int, request_id }` in JSONB `details` — NO raw PII values in log (AC-2, NFR-010).

**Phase 2 — Post-response (de-anonymize):**
6. **Token restoration**: Retrieve redaction map from Redis using `correlationId`. For each token in LLM response text, replace `[REDACTED_*_abc123]` with original value. This restores context references (e.g., "REDACTED_NAME cited in fact...") for downstream use without logging (AC-3).
7. **Log de-anonymization**: Write `AuditService` entry `event_type = "pii_deanonymized"` with `{ correlation_id, tokens_restored: int }` — no raw values (AC-2, NFR-010).
8. **Pipeline failure guard (Edge Case 1)**: Any exception in phase 1 (redaction failure, ACL violation, Redis unavailable) → log `pii_redaction_failed` with `{ correlation_id, error_type }` → throw `PiiRedactionFailureException` → caller returns HTTP 500 / fallback response to client. No raw PII reaches the model.

---

## Dependent Tasks

- **us_053/task_002** — `IAiGatewayClient` and `AiGatewayRequest` must exist; `PiiRedactionService` is called inside the gateway pipeline before `SendAsync`.
- **us_044/task_001** — `AuditService` must exist for redaction event logging.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `IPiiRedactionService` | MODIFY | Extend existing interface (US_049 stub): add `RedactAsync`, `DeAnonymizeAsync`, `GetRedactionMapAsync` |
| `PiiRedactionService` | MODIFY | Full implementation replacing US_049 stub: structured field + NLP free-text redaction; ACL filter; Redis map; audit logging |
| `PiiRedactionOptions` | CREATE | Configuration POCO: `ConfidenceThreshold`, `HmacKey`, `EncryptionKey`, `FieldMappings: string[]`, `MaxRedactionTimeMs` |
| `RedactionContext` | CREATE | Carries `CorrelationId`, `PatientId`, `ClinicianId`, `TokenMap: Dictionary<string,string>` across the pipeline |
| `IRedactionMapStore` | CREATE | Interface: `StoreAsync(correlationId, map)`, `GetAsync(correlationId)`, `DeleteAsync(correlationId)` |
| `RedactionMapStore` | CREATE | Redis with AES-256-GCM encrypted JSON; 5-minute TTL; key: `redaction:{correlationId}` |
| `IPatientContextAclFilter` | CREATE | Interface: `FilterAsync(chunks: IEnumerable<ContextChunk>, patientId, clinicianId)` |
| `PatientContextAclFilter` | CREATE | Validates `chunk.PatientId == patientId`; throws `ACLViolationException` on cross-patient chunk (AC-4) |
| `NlpPiiDetector` | CREATE | Regex + confidence scoring for names, dates, SSN, address, phone patterns (Edge Case 2); configurable threshold |
| `PiiRedactionFailureException` | CREATE | Thrown on pipeline failure; caught by `AiGatewayClient` → returns fallback (Edge Case 1) |
| `ACLViolationException` | CREATE | Thrown when cross-patient context detected; logged + blocks AI call (AC-4) |
| `AiGatewayClient` | MODIFY | Call `IPiiRedactionService.RedactAsync` before `SendAsync`; call `DeAnonymizeAsync` after response (US_053 gateway) |
| `SharedServicesModule` DI | MODIFY | Register `IPiiRedactionService`, `IRedactionMapStore`, `IPatientContextAclFilter`, `NlpPiiDetector`, `PiiRedactionOptions` |

---

## Implementation Plan

1. **Create `PiiRedactionOptions`**: Configuration POCO bound from `IConfiguration.GetSection("AI:Redaction")`. Properties: `double ConfidenceThreshold` (default 0.85), `string HmacKey` (from secrets vault), `string EncryptionKey` (AES-256 key from secrets vault), `string[] StructuredFields` (default: `["patient_name","date_of_birth","ssn","address","phone"]`), `int MaxRedactionTimeMs` (default 500 — pipeline must complete within this window before blocking).
2. **Create `RedactionContext`**: Immutable record: `Guid CorrelationId`, `Guid PatientId`, `Guid ClinicianId`, `Dictionary<string, string> TokenMap` (token → original value), `DateTimeOffset CreatedAt`.
3. **Create `IRedactionMapStore` / `RedactionMapStore`**: `StoreAsync(correlationId, tokenMap)` — serialize `tokenMap` as JSON, encrypt with AES-256-GCM using tenant key, store in Redis as `redaction:{correlationId}` with 5-minute TTL (AC-3). `GetAsync(correlationId)` — decrypt and deserialize. `DeleteAsync` — remove key after successful de-anonymization.
4. **Create `NlpPiiDetector`**: Dictionary of named regex patterns (`patient_name: /\b[A-Z][a-z]+ [A-Z][a-z]+\b/`, `dob: /\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b/`, `ssn: /\b\d{3}-\d{2}-\d{4}\b/`, `phone: /\b\(\d{3}\)\s?\d{3}-\d{4}\b/`, `address: /\b\d{1,5}\s\w+\s(Street|St|Avenue|Ave|Road|Rd|Drive|Dr)\b/i`). For each match: compute confidence score (exact pattern match = 1.0; partial heuristic = 0.7–0.9). Return `List<PiiMatch>` with `FieldType`, `Value`, `Confidence`, `StartIndex`, `Length`. Only matches above `ConfidenceThreshold` proceed to substitution (Edge Case 2).
5. **Implement `IPiiRedactionService.RedactAsync`**: Given `string prompt`, `RedactionContext ctx`: (a) Scan structured fields — for each `fieldName` in `PiiRedactionOptions.StructuredFields`, find pattern `"{fieldName}": "value"` or named placeholder in prompt; generate `HMAC-SHA256(value, hmacKey)[:8]`; build token `[REDACTED_{FIELD}_{hash}]`; replace in prompt; add to `ctx.TokenMap`. (b) Run `NlpPiiDetector` on remaining prompt text; apply token substitution for high-confidence matches; add to `ctx.TokenMap`. (c) Store `ctx.TokenMap` via `IRedactionMapStore.StoreAsync`. (d) Log `pii_redacted` audit entry (AC-2). (e) Return redacted prompt. On any exception → log `pii_redaction_failed` → throw `PiiRedactionFailureException` (Edge Case 1).
6. **Create `IPatientContextAclFilter` / `PatientContextAclFilter`**: Filter `IEnumerable<ContextChunk>` from pgvector retrieval. For each chunk: verify `chunk.PatientId == ctx.PatientId`. If mismatch → log `acl_violation` with chunk details (no raw content) → throw `ACLViolationException` (AC-4). Clinician scope check: verify chunk was retrieved via a query scoped to `patientId` (the HNSW WHERE clause in `EvidenceRetrievalService` already enforces this; this filter is a defence-in-depth check).
7. **Implement `IPiiRedactionService.DeAnonymizeAsync`**: Given `string llmResponseText`, `Guid correlationId`: retrieve `tokenMap` from `IRedactionMapStore.GetAsync`; iterate `tokenMap` replacing each `[REDACTED_*]` token with the original value; delete map from Redis; log `pii_deanonymized` audit entry with `{ correlation_id, tokens_restored: map.Count }` — no raw values (AC-3, AC-2). Return de-anonymized response text.
8. **Modify `AiGatewayClient` (US_053)**: Before `SendAsync` — build `RedactionContext`, call `IPiiRedactionService.RedactAsync(request.Prompt, ctx)` to get redacted prompt; replace in request. Call `IPatientContextAclFilter.FilterAsync` on context chunks. After `SendAsync` — call `IPiiRedactionService.DeAnonymizeAsync(result.Content, correlationId)`. On `PiiRedactionFailureException` or `ACLViolationException` → return `AiGatewayResult { FallbackActive = true, FallbackReason = "PII pipeline failure" }` (Edge Case 1).

---

## Current Project State

```
src/
├── Modules/
│   ├── SharedServices/
│   │   ├── AI/
│   │   │   ├── AiGatewayClient.cs                    ← MODIFY (integrate redaction pipeline)
│   │   │   ├── IPiiRedactionService.cs               ← MODIFY (extend stub from US_049)
│   │   │   ├── PiiRedactionService.cs                ← MODIFY (full implementation)
│   │   │   ├── PiiRedactionOptions.cs                ← CREATE
│   │   │   ├── RedactionContext.cs                   ← CREATE
│   │   │   ├── NlpPiiDetector.cs                     ← CREATE
│   │   │   ├── IRedactionMapStore.cs                 ← CREATE
│   │   │   ├── RedactionMapStore.cs                  ← CREATE (Redis + AES-256-GCM)
│   │   │   ├── IPatientContextAclFilter.cs           ← CREATE
│   │   │   ├── PatientContextAclFilter.cs            ← CREATE
│   │   │   ├── PiiRedactionFailureException.cs       ← CREATE
│   │   │   └── ACLViolationException.cs              ← CREATE
│   │   └── [existing SharedServices structure...]
│   ├── ClinicalIntelligence/
│   │   ├── Services/
│   │   │   ├── PiiRedactionService.cs                ← REMOVE (superseded by SharedServices)
│   │   │   └── [existing services...]
│   │   └── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/SharedServices/AI/PiiRedactionOptions.cs` | Config POCO: ConfidenceThreshold, HmacKey, EncryptionKey, StructuredFields, MaxRedactionTimeMs |
| CREATE | `Modules/SharedServices/AI/RedactionContext.cs` | Immutable record: CorrelationId, PatientId, ClinicianId, TokenMap |
| CREATE | `Modules/SharedServices/AI/NlpPiiDetector.cs` | Regex patterns for name/DOB/SSN/address/phone; confidence scoring (Edge Case 2) |
| CREATE | `Modules/SharedServices/AI/IRedactionMapStore.cs` | Redis-backed token map interface |
| CREATE | `Modules/SharedServices/AI/RedactionMapStore.cs` | AES-256-GCM encrypted Redis store; 5-min TTL (AC-3) |
| CREATE | `Modules/SharedServices/AI/IPatientContextAclFilter.cs` | ACL filter interface (AC-4) |
| CREATE | `Modules/SharedServices/AI/PatientContextAclFilter.cs` | Cross-patient chunk rejection; ACLViolationException (AC-4) |
| CREATE | `Modules/SharedServices/AI/PiiRedactionFailureException.cs` | Thrown on pipeline failure; triggers AI fallback (Edge Case 1) |
| CREATE | `Modules/SharedServices/AI/ACLViolationException.cs` | Thrown on cross-patient context detection (AC-4) |
| MODIFY | `Modules/SharedServices/AI/IPiiRedactionService.cs` | Extend stub: add RedactAsync, DeAnonymizeAsync, GetRedactionMapAsync |
| MODIFY | `Modules/SharedServices/AI/PiiRedactionService.cs` | Full implementation: structured field + NLP redaction + audit logging |
| MODIFY | `Modules/SharedServices/AI/AiGatewayClient.cs` | Integrate redaction before SendAsync; de-anonymize after; handle failure exceptions |

---

## External References

- System.Security.Cryptography AES-GCM: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm
- System.Security.Cryptography HMACSHA256: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256
- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/dotnet/
- AIR-009: Redact direct identifiers from prompts; log redaction actions
- AIR-010: Retrieval ACL filters — only authorized patient-specific context in AI reasoning (AC-4)
- AIR-011: Log prompts, context references, model responses with 7-year retention; redaction audit records must satisfy this
- NFR-010: Immutable audit evidence — `pii_redacted` and `pii_deanonymized` audit events

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

- [ ] Prompt containing `patient_name` field has name replaced with `[REDACTED_NAME_xxxxx]` before reaching LiteLLM (AC-1)
- [ ] SSN pattern `\d{3}-\d{2}-\d{4}` in free text is detected and substituted by `NlpPiiDetector` when confidence ≥ threshold (AC-1, Edge Case 2)
- [ ] `AuditService` record `pii_redacted` written after redaction with `fields_redacted` list and `correlation_id` — no raw values in record (AC-2, NFR-010)
- [ ] LLM response tokens `[REDACTED_*]` replaced with original values by `DeAnonymizeAsync`; `pii_deanonymized` audit entry written (AC-3)
- [ ] Redis redaction map deleted after de-anonymization; 5-minute TTL ensures automatic cleanup (AC-3)
- [ ] Context chunk with `PatientId != requestingPatientId` throws `ACLViolationException`; AI call blocked (AC-4, AIR-010)
- [ ] `PiiRedactionFailureException` (Redis down, HMAC failure) → AI request blocked; no prompt sent to LiteLLM; `pii_redaction_failed` logged with correlation ID (Edge Case 1)
- [ ] Patterns below `ConfidenceThreshold` logged as `pii_detection_low_confidence` but NOT substituted (Edge Case 2)

---

## Implementation Checklist

- [ ] Create `PiiRedactionOptions`, `RedactionContext` records; bind options from `IConfiguration["AI:Redaction"]`
- [ ] Create `NlpPiiDetector` with regex patterns (name, DOB, SSN, address, phone); confidence scoring; configurable threshold (Edge Case 2)
- [ ] Create `IRedactionMapStore` / `RedactionMapStore`: AES-256-GCM encrypted Redis store; 5-min TTL; store/get/delete operations (AC-3)
- [ ] Create `IPatientContextAclFilter` / `PatientContextAclFilter`: per-chunk `patient_id` validation; `ACLViolationException` on violation (AC-4, AIR-010)
- [ ] Create `PiiRedactionFailureException`, `ACLViolationException` custom exceptions
- [ ] Implement full `IPiiRedactionService.RedactAsync`: structured field + NLP substitution; token map storage; `pii_redacted` audit entry; `PiiRedactionFailureException` on any failure (AC-1, AC-2, Edge Case 1)
- [ ] Implement `IPiiRedactionService.DeAnonymizeAsync`: token restoration from Redis; Redis cleanup; `pii_deanonymized` audit entry (AC-3)
- [ ] Modify `AiGatewayClient` (US_053): call `RedactAsync` before `SendAsync`; call `ACLFilter` on context; call `DeAnonymizeAsync` after response; handle failure exceptions → fallback result (Edge Case 1)

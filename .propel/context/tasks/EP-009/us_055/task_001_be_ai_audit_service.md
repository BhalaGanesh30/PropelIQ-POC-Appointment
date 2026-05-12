---
task_id: task_001
user_story: us_055
epic: EP-009
layer: Backend
status: done
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_055] AI Prompt and Response Audit Logging
- **Story Location**: [.propel/context/tasks/EP-009/us_055/us_055.md](.propel/context/tasks/EP-009/us_055/us_055.md)
- **Acceptance Criteria**:
  - AC-1: Given an AI request is processed, When the request completes, Then a structured audit record is written containing: request ID, timestamp, clinician identity, prompt hash, context document references, model name, response payload, confidence scores, and latency.
  - AC-2: Given a clinician makes a coding decision (accept/modify/reject), When the decision is recorded, Then the decision outcome is appended to the AI audit record for the associated suggestion request.
  - AC-3: Given the audit record is written, When it is stored, Then it is persisted in the append-only audit table with no UPDATE or DELETE permissions and a 7-year retention policy enforced.
  - AC-4: Given an admin queries the AI audit log, When they filter by date range and clinician, Then all matching records are returned with full structured metadata.
- **Edge Cases**:
  - Edge Case 1: Audit log write fails after AI response returned — AI response is still returned to caller; a compensating async outbox write is retried via background service; unresolvable failures (> 3 retries) raise a `compliance.audit_write_failure` OpenTelemetry alert.
  - Edge Case 2: Long-term storage growth — partitioning and cold storage are a DB-layer concern (task_002); the service layer is partition-transparent.

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
| AI Gateway | IAiGatewayClient (US_053) | latest stable |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-011 |
| **AI Pattern** | Post-inference audit trail; append-only structured AI evidence log |
| **Prompt Template Path** | N/A — audit logging is a cross-cutting concern, not a prompt pattern |
| **Guardrails Config** | N/A |
| **Model Provider** | Azure OpenAI GPT-4.1 (called via `IAiGatewayClient`) |

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

Implement the dedicated `AiAuditService` in the `SharedServices` module to satisfy AIR-011 (structured AI audit records with 7-year retention) and NFR-010 (immutable audit evidence). This service is a specialized complement to the existing general-purpose `AuditService` — the general `audit_records` table handles access/override events while `ai_audit_logs` (task_002) stores AI-specific structured evidence including prompt hashes, context references, model responses, and confidence scores.

Two integration points are required:

1. **`AiGatewayClient`** (US_053): After each AI response, call `IAiAuditService.LogAiRequestAsync(...)` with all AIR-011 fields. The call is **fire-and-forget with outbox fallback** — the AI response is returned to the caller immediately without waiting for the audit write. If the write fails, the record is inserted into `ai_audit_outbox` for retry (Edge Case 1).

2. **`CodingDecisionService`** (US_051): When accept/modify/reject is recorded, call `IAiAuditService.AppendReviewerOutcomeAsync(aiRequestId, reviewerAction, reviewerNote)` which issues a targeted INSERT to append the outcome row to `ai_audit_log_outcomes` (a sister append-only table linked by `ai_request_id` — no UPDATE on the base record, preserving append-only constraint).

The admin query endpoint `GET /api/v1/admin/audit-logs` already exists for general audit records; this task extends it with an `?eventType=ai_request` filter variant that queries `ai_audit_logs` with `clinicianId` and date-range predicates (AC-4).

---

## Dependent Tasks

- **us_055/task_002** — `ai_audit_logs` and `ai_audit_outbox` tables must exist; `ai_audit_log_outcomes` sister table must exist before `AiAuditService` is executable.
- **us_053/task_002** — `IAiGatewayClient` must exist; `AiGatewayRequest` and `AiGatewayResult` DTOs supply the fields logged by `LogAiRequestAsync`.
- **us_054/task_001** — `IPiiRedactionService.RedactAsync` must have run before audit logging so that `promptHash` is computed from the **redacted** prompt (not raw PII), satisfying AIR-009 + AIR-011 together.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `IAiAuditService` | CREATE | Interface: `LogAiRequestAsync(AiAuditEntry)`, `AppendReviewerOutcomeAsync(Guid, string, string?)` |
| `AiAuditService` | CREATE | EF Core writes to `ai_audit_logs`; outbox fallback on failure; OpenTelemetry counter on success and failure |
| `AiAuditEntry` | CREATE | Request DTO: all AIR-011 fields (see Implementation Plan step 1) |
| `AiAuditOutboxProcessor` | CREATE | `IHostedService` background worker: polls `ai_audit_outbox`, retries failed writes; `compliance.audit_write_failure` alert after 3 attempts (Edge Case 1) |
| `AiAuditLogEntity` | CREATE | EF Core entity mapping to `ai_audit_logs` table (task_002) |
| `AiAuditOutboxEntity` | CREATE | EF Core entity mapping to `ai_audit_outbox` table (task_002) |
| `AiAuditLogOutcomeEntity` | CREATE | EF Core entity mapping to `ai_audit_log_outcomes` table (task_002) |
| `AiGatewayClient` | MODIFY | Call `IAiAuditService.LogAiRequestAsync` after AI response; fire-and-forget with exception catch → outbox insertion (US_053) |
| `CodingDecisionService` | MODIFY | Call `IAiAuditService.AppendReviewerOutcomeAsync` after accept/modify/reject persisted (US_051) |
| `AdminAuditController` | MODIFY | Extend `GET /api/v1/admin/audit-logs` with `?eventType=ai_request` — queries `ai_audit_logs` with `clinicianId` + `from` + `to` + pagination (AC-4); `[Authorize(Roles = "Admin")]` |
| `AiAuditLogQueryDto` | CREATE | Response DTO for admin query: all AIR-011 fields + reviewer outcome if present |
| `SharedServicesModule` DI | MODIFY | Register `IAiAuditService`, `AiAuditOutboxProcessor` (hosted service), `AiAuditLogQueryDto` |

---

## Implementation Plan

1. **Define `AiAuditEntry`**: Immutable record with all AIR-011 fields:
   - `Guid AiRequestId` — caller-supplied correlation ID (same as `CorrelationId` from `RedactionContext`)
   - `DateTimeOffset RequestTimestamp`
   - `Guid ClinicianId` — extracted from `HttpContext.User.GetUserId()`
   - `string PromptHash` — `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(redactedPrompt)))` — SHA-256 of redacted prompt (NOT raw PII per AIR-009)
   - `JsonDocument ContextRefs` — serialized list of `{ documentId, chunkId, similarity }` from retrieval context
   - `string ModelName` — e.g., `"gpt-4.1"` from `AiGatewayRequest.ModelId`
   - `JsonDocument ResponsePayload` — full LLM response JSON (already redacted via `PiiRedactionService`)
   - `JsonDocument ConfidenceScores` — e.g., `{ "ICD-10": 0.91, "CPT": 0.88 }` extracted from AI response
   - `int LatencyMs` — milliseconds from request start to response complete
   - `string? FallbackReason` — populated if `AiGatewayResult.FallbackActive = true`

2. **Create `IAiAuditService` / `AiAuditService`**:
   - `LogAiRequestAsync(AiAuditEntry entry, CancellationToken ct)`: Map `AiAuditEntry` → `AiAuditLogEntity`; call `dbContext.AiAuditLogs.Add(entity)` + `SaveChangesAsync`. On `DbUpdateException` or `OperationCanceledException` → serialize entry as JSON → insert to `ai_audit_outbox` table with `retry_count = 0`; increment `compliance.audit_write_failure_count` OpenTelemetry counter. On success → increment `compliance.ai_audit_written_count` counter (AC-1, Edge Case 1).
   - `AppendReviewerOutcomeAsync(Guid aiRequestId, string reviewerAction, string? reviewerNote, CancellationToken ct)`: INSERT new row to `ai_audit_log_outcomes` (not UPDATE on base record — preserves append-only constraint of `ai_audit_logs`). Fields: `ai_request_id`, `reviewer_action`, `reviewer_note`, `decided_at = NOW()` (AC-2).

3. **Create `AiAuditOutboxProcessor` (`IHostedService`)**:
   - Polls `ai_audit_outbox WHERE retry_count < 3 AND (last_attempt_at IS NULL OR last_attempt_at < NOW() - INTERVAL '2 minutes')` every 60 seconds.
   - For each pending row: deserialize `payload` JSON → reconstruct `AiAuditEntry` → call `AiAuditService.LogAiRequestAsync`. On success → `DELETE FROM ai_audit_outbox WHERE outbox_id = X`. On failure → `UPDATE ai_audit_outbox SET retry_count = retry_count + 1, last_attempt_at = NOW()`. When `retry_count >= 3` → increment `compliance.audit_write_failure` OpenTelemetry counter and emit structured log `LogError("AI audit record unresolvable: {CorrelationId}", ...)` (Edge Case 1).

4. **Modify `AiGatewayClient`** (US_053):
   - After `AiGatewayResult` is returned: start a `Task.Run(() => _auditService.LogAiRequestAsync(entry))` fire-and-forget wrapped in `try/catch`; exceptions caught here insert to outbox directly (avoid unobserved task exceptions).
   - Populate `AiAuditEntry` using fields from `AiGatewayRequest` (ModelId, PromptHash, ContextRefs) and `AiGatewayResult` (ResponsePayload, ConfidenceScores, LatencyMs, FallbackReason).

5. **Modify `CodingDecisionService`** (US_051):
   - After persisting `CodingDecision` accept/modify/reject: call `await _auditService.AppendReviewerOutcomeAsync(decision.AiRequestId, decision.ReviewerAction.ToString(), decision.ReviewerNote)`. `AiRequestId` is stored on `CodingDecision` (add nullable FK column if not present — additive migration handled in task_002).

6. **Extend admin query — `AdminAuditController`**:
   - Add `AiAuditLogsQueryParameters` model: `DateTimeOffset? From`, `DateTimeOffset? To`, `Guid? ClinicianId`, `int PageSize = 50`, `int Page = 1`.
   - Add action `[HttpGet("audit-logs/ai")]` → `[Authorize(Roles = "Admin")]`; query `ai_audit_logs` with EF Core: `WHERE (@ClinicianId IS NULL OR clinician_id = @ClinicianId) AND (@From IS NULL OR request_timestamp >= @From) AND (@To IS NULL OR request_timestamp <= @To)` ordered by `request_timestamp DESC` with `Skip/Take` pagination; left-join `ai_audit_log_outcomes` by `ai_request_id` to include reviewer outcome if present.
   - Return `PagedResult<AiAuditLogQueryDto>` — `AiAuditLogQueryDto` maps all AIR-011 fields plus `ReviewerAction`/`ReviewerNote`/`ReviewerOutcomeAt` from the outcomes join (AC-4).

7. **OpenTelemetry metrics**:
   - `compliance.ai_audit_written_count` counter — incremented on every successful `LogAiRequestAsync`
   - `compliance.audit_write_failure_count` counter — incremented on primary write failure (routes to outbox)
   - `compliance.audit_write_failure` counter — incremented when outbox retry exhausted (> 3 attempts); this counter triggers operations alerting (Edge Case 1)
   - Register `MeterProvider` in `SharedServicesModule`.

---

## Current Project State

```
src/
├── Modules/
│   ├── SharedServices/
│   │   ├── AI/
│   │   │   ├── AiGatewayClient.cs                    ← MODIFY (fire-and-forget audit call)
│   │   │   ├── IAiAuditService.cs                    ← CREATE
│   │   │   ├── AiAuditService.cs                     ← CREATE
│   │   │   ├── AiAuditEntry.cs                       ← CREATE
│   │   │   ├── AiAuditLogQueryDto.cs                 ← CREATE
│   │   │   └── AiAuditOutboxProcessor.cs             ← CREATE (IHostedService)
│   │   ├── Data/
│   │   │   ├── Entities/
│   │   │   │   ├── AiAuditLogEntity.cs               ← CREATE
│   │   │   │   ├── AiAuditOutboxEntity.cs            ← CREATE
│   │   │   │   └── AiAuditLogOutcomeEntity.cs        ← CREATE
│   │   │   └── Migrations/
│   │   │       └── [EF Core migration for ai_audit_logs FK on coding_decisions] ← CREATE
│   │   └── [existing SharedServices structure...]
│   ├── ClinicalIntelligence/
│   │   ├── Services/
│   │   │   └── CodingDecisionService.cs              ← MODIFY (call AppendReviewerOutcomeAsync)
│   │   └── [existing structure...]
├── Api/
│   ├── Controllers/
│   │   └── AdminAuditController.cs                   ← MODIFY (add GET audit-logs/ai endpoint)
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/SharedServices/AI/IAiAuditService.cs` | Interface: LogAiRequestAsync, AppendReviewerOutcomeAsync |
| CREATE | `Modules/SharedServices/AI/AiAuditService.cs` | EF Core writes to ai_audit_logs + outbox fallback on failure (Edge Case 1) |
| CREATE | `Modules/SharedServices/AI/AiAuditEntry.cs` | All AIR-011 fields: AiRequestId, PromptHash, ContextRefs, ModelName, ResponsePayload, ConfidenceScores, LatencyMs |
| CREATE | `Modules/SharedServices/AI/AiAuditLogQueryDto.cs` | Admin query response DTO: all AIR-011 fields + reviewer outcome fields (AC-4) |
| CREATE | `Modules/SharedServices/AI/AiAuditOutboxProcessor.cs` | IHostedService; 60s poll; retry ≤ 3; compliance.audit_write_failure alert on exhaustion (Edge Case 1) |
| CREATE | `Modules/SharedServices/Data/Entities/AiAuditLogEntity.cs` | EF Core entity: ai_audit_logs |
| CREATE | `Modules/SharedServices/Data/Entities/AiAuditOutboxEntity.cs` | EF Core entity: ai_audit_outbox |
| CREATE | `Modules/SharedServices/Data/Entities/AiAuditLogOutcomeEntity.cs` | EF Core entity: ai_audit_log_outcomes |
| MODIFY | `Modules/SharedServices/AI/AiGatewayClient.cs` | Fire-and-forget audit call after AI response; exception → outbox insert |
| MODIFY | `Modules/ClinicalIntelligence/Services/CodingDecisionService.cs` | Call AppendReviewerOutcomeAsync after accept/modify/reject persisted (AC-2) |
| MODIFY | `Api/Controllers/AdminAuditController.cs` | Add GET /api/v1/admin/audit-logs/ai with date range + clinician filter + pagination (AC-4) |

---

## External References

- SHA-256 hashing (.NET 8): https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256
- EF Core 8 — append-only patterns: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/whatsnew
- OpenTelemetry .NET Metrics: https://opentelemetry.io/docs/languages/dotnet/instrumentation/#creating-metrics
- IHostedService background tasks: https://learn.microsoft.com/en-us/dotnet/core/extensions/hosted-services
- AIR-011: Log prompts, context references, model responses, confidence values, and reviewer outcomes with 7-year retention
- NFR-010: Immutable audit evidence for access events, coding decisions, and overrides with minimum 7-year retention
- DR-005: Retain immutable audit and access logs for 7 years with append-only write constraints

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

# Apply EF Core migration
dotnet ef database update --project src/Data --startup-project src/Api

# Run the API
dotnet run --project src/Api/Api.csproj
```

---

## Implementation Validation Strategy

- [ ] After AI request completes, `ai_audit_logs` row exists with all AIR-011 fields populated (AC-1); `prompt_hash` is SHA-256 of redacted prompt — no raw PII (AIR-009)
- [ ] `ContextRefs` column contains serialized evidence chunk references; `ModelName` = `"gpt-4.1"`; `ConfidenceScores` populated from AI response (AC-1)
- [ ] After clinician accepts/modifies/rejects, `ai_audit_log_outcomes` row inserted with `ai_request_id` FK; base `ai_audit_logs` row is NOT updated (AC-2, AC-3)
- [ ] Simulated DB failure during `LogAiRequestAsync` → `ai_audit_outbox` row created; `compliance.audit_write_failure_count` counter incremented; AI response still returned to caller (Edge Case 1)
- [ ] `AiAuditOutboxProcessor` retries outbox rows at 60s interval; row deleted on success; after 3 failures → `compliance.audit_write_failure` counter incremented (Edge Case 1)
- [ ] `GET /api/v1/admin/audit-logs/ai?from=2026-01-01&to=2026-12-31&clinicianId=...` returns matching records with full metadata including reviewer outcome (AC-4); returns 403 for non-Admin roles
- [ ] `[Authorize(Roles = "Admin")]` enforced on all audit query endpoints (AC-4)

---

## Implementation Checklist

- [x] Create `AiAuditEntry` record with all AIR-011 fields; `PromptHash` computed from SHA-256 of redacted prompt (AC-1, AIR-009)
- [x] Create `IAiAuditService` / `AiAuditService`: `LogAiRequestAsync` (EF Core → `ai_audit_logs`; on failure → outbox insertion); `AppendReviewerOutcomeAsync` (INSERT to `ai_audit_log_outcomes`) (AC-1, AC-2, AC-3)
- [x] Create EF Core entities for `ai_audit_logs`, `ai_audit_outbox`, `ai_audit_log_outcomes`; configure append-only conventions (no `HasMany` update navigations)
- [x] Create `AiAuditOutboxProcessor` (`IHostedService`): 60s poll; retry ≤ 3; `compliance.audit_write_failure` OTel counter on exhaustion (Edge Case 1)
- [x] Modify `AiGatewayClient` (US_053): fire-and-forget `LogAiRequestAsync`; catch exception → outbox insert (AC-1, Edge Case 1)
- [x] Modify `CodingDecisionService` (US_051): call `AppendReviewerOutcomeAsync` after each decision persisted (AC-2)
- [x] Add `GET /api/v1/audit/audit-logs/ai` to `AuditController`: `[Authorize(Roles = "Admin")]`; date-range + clinicianId filter + pagination (AC-4)
- [x] Register `IAiAuditService`, `AiAuditOutboxProcessor` (hosted service), OTel meters in `SharedServicesModule` DI

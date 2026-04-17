---
task_id: task_001
user_story: us_044
epic: EP-007
layer: Backend
status: not-started
effort_hours: 10
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_044] Clinical Entity Extraction with Confidence Scoring
- **Story Location**: [.propel/context/tasks/EP-007/us_044/us_044.md](.propel/context/tasks/EP-007/us_044/us_044.md)
- **Acceptance Criteria**:
  - AC-1: Given an OCR-processed document is available, When the clinical extraction pipeline runs, Then medications, allergies, diagnoses, and free-text findings are extracted as structured ClinicalFact records with individual confidence scores.
  - AC-2: Given an extracted clinical fact is stored, When I view it in the patient profile, Then the confidence score is displayed alongside a source document reference link.
  - AC-3: Given an extracted fact has a confidence score below the configured threshold (e.g., 70%), When it is displayed, Then a "Low Confidence – Review Required" indicator is shown to prompt manual verification.
  - AC-4: Given extraction completes, When the results are validated by the schema validator, Then at least 99% of extraction payloads pass schema validation before being stored.
- **Edge Cases**:
  - Edge Case 1: OCR text quality too low for meaningful extraction — extraction returns empty results with a `LowInputQuality` flag; document is flagged for manual review.
  - Edge Case 2: Conflicting extractions from multiple documents — each extraction is stored independently with source document reference; conflict detection is deferred to US_046.

---

## Design References (Backend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A (backend + AI task) |
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
| Database | PostgreSQL with pgvector | 15.x |
| AI/ML | Azure OpenAI GPT-4.1 | 2026 APIs |
| Vector Store | pgvector (PostgreSQL extension) | 0.7.x |
| AI Gateway | LiteLLM-compatible gateway | latest |
| Embedding Model | text-embedding-3-small | latest |
| Guardrails | JSON schema validation + policy filters | N/A |
| Queue | System.Threading.Channels | 8.x |
| Observability | OpenTelemetry | latest |
| Frontend | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-001, AIR-004, AIR-005, AIR-008, AIR-009, AIR-010 |
| **AI Pattern** | Hybrid (model inference + deterministic normalization) |
| **Prompt Template Path** | `prompts/clinical-extraction/` |
| **Guardrails Config** | `config/extraction-schema.json` |
| **Model Provider** | Azure OpenAI (GPT-4.1 family via LiteLLM gateway) |

### CRITICAL: AI Implementation Requirement (AI Tasks Only)

**IF AI Impact = Yes:**
- **MUST** reference prompt templates from Prompt Template Path during implementation
- **MUST** implement guardrails for input sanitization and output validation
- **MUST** enforce token budget limits per AIR-001 requirements
- **MUST** implement fallback logic for low-confidence responses (AIR-005)
- **MUST** log all prompts/responses for audit (redact PII per AIR-009)
- **MUST** handle model failures gracefully (timeout, rate limit, 5xx errors) with circuit-breaker (TR-008)

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

Implement the clinical entity extraction pipeline within the `ClinicalIntelligence` module. After OCR processing completes (US_041), the extraction pipeline consumes the `extracted_text` from `clinical_documents`, sends it through an AI gateway (LiteLLM) to Azure OpenAI GPT-4.1 with a structured extraction prompt, and persists the extracted entities as `clinical_facts` records with individual confidence scores. The pipeline follows a hybrid pattern (AIR-001): (1) AI model inference extracts medications, allergies, diagnoses, and free-text findings with confidence scores; (2) deterministic normalization rules standardize drug names, allergy terminology, and diagnosis codes. Input text is chunked and PII-redacted before prompt submission (AIR-009). Output payloads are validated against a JSON schema with a 99% pass rate target (AIR-008, AC-4). Facts with confidence below a configurable threshold (default 70%) are flagged with `needs_review = true` (AC-3, AIR-005). If OCR text quality is too low (Edge Case 1), the pipeline returns empty results with a `LowInputQuality` flag and the document is flagged for manual review. Each fact stores a `document_id` reference for source traceability (AIR-004, AC-2). The pipeline integrates with the existing `OcrWorkerService` (US_041) — after OCR completes, an extraction job is enqueued. Circuit-breaker fallback to deterministic flows is enforced when the AI gateway is unavailable (TR-008, AIR-005).

---

## Dependent Tasks

- **us_041/task_002** — OCR worker must populate `extracted_text` in `clinical_documents` and signal extraction readiness.
- **us_044/task_002** — `clinical_facts` table with `confidence_score`, `needs_review`, `fact_type` enum, and pgvector embedding column must be migrated.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ExtractionJob` | CREATE | Record: `Guid DocumentId`, `Guid PatientId`, `string ExtractedText` |
| `ExtractionJobChannel` | CREATE | Singleton bounded Channel for extraction jobs (reuses pattern from OcrJobChannel) |
| `IClinicalExtractionService` | CREATE | Interface: `ExtractEntitiesAsync(ExtractionJob, CancellationToken)` |
| `ClinicalExtractionService` | CREATE | Orchestrates: chunk → redact PII → prompt LLM → validate → normalize → persist |
| `IAiGatewayClient` | CREATE | Interface wrapping LiteLLM-compatible HTTP calls with circuit-breaker |
| `AiGatewayClient` | CREATE | HttpClient-based LLM calls with retry, timeout, circuit-breaker (Polly) |
| `AiGatewayConfiguration` | CREATE | Options: `BaseUrl`, `ApiKey`, `ModelId`, `TimeoutSeconds`, `MaxTokens` |
| `IPromptBuilder` | CREATE | Interface: build extraction prompt from chunked text |
| `ExtractionPromptBuilder` | CREATE | Constructs structured extraction prompt with system message, few-shot examples, output schema |
| `IPiiRedactionService` | CREATE | Interface: redact direct identifiers from text before prompting |
| `PiiRedactionService` | CREATE | Regex + NER-based PII redaction; logs redaction actions (AIR-009) |
| `IExtractionSchemaValidator` | CREATE | Interface: validate LLM output against JSON schema |
| `ExtractionSchemaValidator` | CREATE | JSON schema validation; tracks pass/fail rate metric (AIR-008) |
| `INormalizationService` | CREATE | Interface: standardize drug names, allergy terms, diagnosis codes |
| `NormalizationService` | CREATE | Deterministic rules: drug name normalization (RxNorm-style), allergy standardization, diagnosis code mapping |
| `ExtractionResult` | CREATE | Record: `List<ExtractedFact> Facts`, `bool LowInputQuality`, `int SchemaValidationPassCount`, `int SchemaValidationTotalCount` |
| `ExtractedFact` | CREATE | Record: `string FactType`, `string Name`, `string Value`, `decimal Confidence`, `string SourceText` |
| `ExtractionWorkerService` | CREATE | BackgroundService consuming from ExtractionJobChannel; retry with dead-letter |
| `ExtractionConfiguration` | CREATE | Options: `ConfidenceThreshold`, `MaxChunkSize`, `ConcurrencyLimit`, `MaxRetries` |
| `OcrWorkerService` | MODIFY | After OCR completes and extraction_status = Completed, enqueue `ExtractionJob` to `ExtractionJobChannel` |
| `ClinicalFact` (EF entity) | CREATE | EF entity mapping to `clinical_facts` table |
| `IClinicalFactRepository` | CREATE | Repository: `AddRangeAsync`, `GetByDocumentIdAsync`, `GetByPatientIdAsync` |
| `ClinicalFactRepository` | CREATE | EF Core implementation |
| `ClinicalIntelligenceModule` DI | MODIFY | Register all new services |

---

## Implementation Plan

1. **Create `AiGatewayConfiguration`** options class: `BaseUrl` (string), `ApiKey` (string, from secrets), `ModelId` (string, default `"gpt-4.1"`), `TimeoutSeconds` (int, default `30`), `MaxTokens` (int, default `4096`), `EmbeddingModelId` (string, default `"text-embedding-3-small"`). Bind from `appsettings.json` section `AiGateway`.
2. **Create `ExtractionConfiguration`** options class: `ConfidenceThreshold` (decimal, default `0.70`), `MaxChunkSize` (int, default `4000` tokens), `ConcurrencyLimit` (int, default `2`), `MaxRetries` (int, default `3`), `LowQualityTextLengthThreshold` (int, default `50` characters). Bind from `appsettings.json` section `Extraction`.
3. **Create `IAiGatewayClient` and `AiGatewayClient`**: Interface with `Task<string> CompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct)` and `Task<float[]> EmbedAsync(string text, CancellationToken ct)`. Implementation uses `HttpClient` to call LiteLLM-compatible `/chat/completions` and `/embeddings` endpoints. Apply Polly circuit-breaker policy: break after 5 consecutive failures, 30-second recovery. On circuit open, throw `AiGatewayUnavailableException` to trigger deterministic fallback (TR-008, AIR-005). Apply retry with exponential backoff for transient errors (429, 503). Emit OpenTelemetry metrics: `ai.gateway.requests`, `ai.gateway.latency_ms`, `ai.gateway.errors`.
4. **Create `IPiiRedactionService` and `PiiRedactionService`**: Redact SSN patterns (`\d{3}-\d{2}-\d{4}`), phone numbers, email addresses, dates of birth (contextual), and full names (from patient context) using regex patterns. Log each redaction action with field type and character range (AIR-009). Replace redacted text with `[REDACTED_TYPE]` tokens. Return both redacted text and a redaction map for audit logging.
5. **Create `IPromptBuilder` and `ExtractionPromptBuilder`**: Build a structured extraction prompt with: (a) system message defining the extraction task and output JSON schema; (b) few-shot examples (2-3) showing input text → expected JSON output for medications, allergies, diagnoses, findings; (c) user message containing the PII-redacted chunked text; (d) explicit instruction to return confidence scores per entity (0.0–1.0). Store prompt templates in `prompts/clinical-extraction/system.txt` and `prompts/clinical-extraction/examples.json`. Token budget: system prompt + examples < 1500 tokens, user content < `MaxChunkSize` tokens, total < `MaxTokens`.
6. **Create `IExtractionSchemaValidator` and `ExtractionSchemaValidator`**: Define a JSON schema for extraction output: `{ facts: [{ fact_type: enum, name: string, value: string, confidence: number, source_text: string }] }`. Validate each LLM response against the schema using `System.Text.Json` deserialization with strict validation. Track cumulative pass/fail counts and emit `extraction.schema.pass_rate` metric. If pass rate drops below 99% over a rolling window, log a warning (AIR-008, AC-4).
7. **Create `INormalizationService` and `NormalizationService`**: Deterministic post-processing rules: (a) normalize medication names to lowercase canonical forms (e.g., "Tylenol" → "acetaminophen/tylenol"); (b) standardize allergy terminology (e.g., "PCN allergy" → "penicillin allergy"); (c) map diagnosis text to potential ICD-10 prefixes where possible (lookup table); (d) trim whitespace and normalize punctuation in values. These rules run after AI extraction, supplementing model output (AIR-001 hybrid pattern).
8. **Create `IClinicalExtractionService` and `ClinicalExtractionService`**: Orchestrates the full pipeline for a single document:
   - (a) Check text quality: if `extractedText.Length < LowQualityTextLengthThreshold`, return `ExtractionResult { LowInputQuality = true, Facts = [] }` and flag document `needs_manual_review = true` (Edge Case 1).
   - (b) Chunk text into segments of `MaxChunkSize` tokens using a sentence-boundary-aware splitter.
   - (c) For each chunk: redact PII via `IPiiRedactionService` → build prompt via `IPromptBuilder` → call `IAiGatewayClient.CompletionAsync()` → validate response via `IExtractionSchemaValidator` → if valid, parse facts; if invalid, log and skip chunk.
   - (d) Aggregate facts from all chunks, deduplicate by (fact_type, name, value).
   - (e) Apply normalization via `INormalizationService`.
   - (f) For each fact, if `confidence < ConfidenceThreshold`, set `NeedsReview = true` (AC-3, AIR-005).
   - (g) Generate embeddings for each fact via `IAiGatewayClient.EmbedAsync()` for future RAG retrieval.
   - (h) Persist all facts via `IClinicalFactRepository.AddRangeAsync()` with `document_id` reference (AIR-004, AC-2, Edge Case 2).
   - (i) Return `ExtractionResult` with facts, quality flag, and schema validation stats.
   - On `AiGatewayUnavailableException`: fall back to empty extraction with `document.needs_manual_review = true`; log circuit-breaker event (AIR-005, TR-008).
9. **Create `ExtractionWorkerService`** as a `BackgroundService`: consume from `ExtractionJobChannel` with `ExtractionConfiguration.ConcurrencyLimit` concurrent consumers. For each job, call `IClinicalExtractionService.ExtractEntitiesAsync()`. On failure, retry with exponential backoff (up to `MaxRetries`). On exhausted retries, write to dead-letter queue and set `extraction_status = Failed`. On success, update `extraction_status = Completed` in `clinical_documents`. Use `IServiceScopeFactory` for scoped services.
10. **Modify `OcrWorkerService`**: After successful OCR (extraction_status = Completed), enqueue `new ExtractionJob(document.DocumentId, document.PatientId, document.ExtractedText)` to `ExtractionJobChannel.Writer.WriteAsync()`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Configuration/
│   │   │   ├── ClamAvConfiguration.cs                ← EXISTS (US_040)
│   │   │   ├── OcrConfiguration.cs                   ← EXISTS (US_041)
│   │   │   ├── AiGatewayConfiguration.cs             ← CREATE
│   │   │   └── ExtractionConfiguration.cs            ← CREATE
│   │   ├── AI/
│   │   │   ├── IAiGatewayClient.cs                   ← CREATE
│   │   │   ├── AiGatewayClient.cs                    ← CREATE
│   │   │   ├── IPromptBuilder.cs                     ← CREATE
│   │   │   ├── ExtractionPromptBuilder.cs            ← CREATE
│   │   │   ├── IPiiRedactionService.cs               ← CREATE
│   │   │   ├── PiiRedactionService.cs                ← CREATE
│   │   │   ├── IExtractionSchemaValidator.cs         ← CREATE
│   │   │   └── ExtractionSchemaValidator.cs          ← CREATE
│   │   ├── Services/
│   │   │   ├── IClinicalExtractionService.cs         ← CREATE
│   │   │   ├── ClinicalExtractionService.cs          ← CREATE
│   │   │   ├── INormalizationService.cs              ← CREATE
│   │   │   └── NormalizationService.cs               ← CREATE
│   │   ├── Models/
│   │   │   ├── ExtractionJob.cs                      ← CREATE
│   │   │   ├── ExtractionResult.cs                   ← CREATE
│   │   │   └── ExtractedFact.cs                      ← CREATE
│   │   ├── Queues/
│   │   │   ├── OcrJobChannel.cs                      ← EXISTS (US_041)
│   │   │   └── ExtractionJobChannel.cs               ← CREATE
│   │   ├── Workers/
│   │   │   ├── OcrWorkerService.cs                   ← MODIFY (enqueue extraction after OCR)
│   │   │   └── ExtractionWorkerService.cs            ← CREATE
│   │   ├── Entities/
│   │   │   └── ClinicalFact.cs                       ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalFactRepository.cs            ← CREATE
│   │   │   └── ClinicalFactRepository.cs             ← CREATE
│   │   └── Prompts/
│   │       └── clinical-extraction/
│   │           ├── system.txt                        ← CREATE
│   │           └── examples.json                     ← CREATE
│   └── [existing modules...]
└── [existing project structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Configuration/AiGatewayConfiguration.cs` | Options: BaseUrl, ApiKey, ModelId, TimeoutSeconds, MaxTokens, EmbeddingModelId |
| CREATE | `Modules/ClinicalIntelligence/Configuration/ExtractionConfiguration.cs` | Options: ConfidenceThreshold, MaxChunkSize, ConcurrencyLimit, MaxRetries |
| CREATE | `Modules/ClinicalIntelligence/AI/IAiGatewayClient.cs` | LLM gateway interface: CompletionAsync, EmbedAsync |
| CREATE | `Modules/ClinicalIntelligence/AI/AiGatewayClient.cs` | HttpClient + Polly circuit-breaker + retry for LiteLLM gateway |
| CREATE | `Modules/ClinicalIntelligence/AI/IPromptBuilder.cs` | Prompt construction interface |
| CREATE | `Modules/ClinicalIntelligence/AI/ExtractionPromptBuilder.cs` | Structured extraction prompt with system message, few-shot, output schema |
| CREATE | `Modules/ClinicalIntelligence/AI/IPiiRedactionService.cs` | PII redaction interface |
| CREATE | `Modules/ClinicalIntelligence/AI/PiiRedactionService.cs` | Regex + contextual PII redaction with audit logging (AIR-009) |
| CREATE | `Modules/ClinicalIntelligence/AI/IExtractionSchemaValidator.cs` | JSON schema validation interface |
| CREATE | `Modules/ClinicalIntelligence/AI/ExtractionSchemaValidator.cs` | Strict JSON validation with pass rate tracking (AIR-008) |
| CREATE | `Modules/ClinicalIntelligence/Services/IClinicalExtractionService.cs` | Extraction orchestration interface |
| CREATE | `Modules/ClinicalIntelligence/Services/ClinicalExtractionService.cs` | Full pipeline: quality check → chunk → redact → prompt → validate → normalize → persist |
| CREATE | `Modules/ClinicalIntelligence/Services/INormalizationService.cs` | Deterministic normalization interface |
| CREATE | `Modules/ClinicalIntelligence/Services/NormalizationService.cs` | Drug name, allergy, diagnosis normalization rules |
| CREATE | `Modules/ClinicalIntelligence/Models/ExtractionJob.cs` | Record: DocumentId, PatientId, ExtractedText |
| CREATE | `Modules/ClinicalIntelligence/Models/ExtractionResult.cs` | Record: Facts, LowInputQuality, SchemaValidationPassCount/TotalCount |
| CREATE | `Modules/ClinicalIntelligence/Models/ExtractedFact.cs` | Record: FactType, Name, Value, Confidence, SourceText |
| CREATE | `Modules/ClinicalIntelligence/Queues/ExtractionJobChannel.cs` | Bounded Channel for extraction jobs |
| CREATE | `Modules/ClinicalIntelligence/Workers/ExtractionWorkerService.cs` | BackgroundService: concurrent extraction consumers with retry/dead-letter |
| CREATE | `Modules/ClinicalIntelligence/Entities/ClinicalFact.cs` | EF entity mapping to `clinical_facts` table |
| CREATE | `Modules/ClinicalIntelligence/Repositories/IClinicalFactRepository.cs` | Repository: AddRangeAsync, GetByDocumentIdAsync, GetByPatientIdAsync |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ClinicalFactRepository.cs` | EF Core implementation |
| CREATE | `Modules/ClinicalIntelligence/Prompts/clinical-extraction/system.txt` | System prompt template for extraction |
| CREATE | `Modules/ClinicalIntelligence/Prompts/clinical-extraction/examples.json` | Few-shot examples for extraction |
| MODIFY | `Modules/ClinicalIntelligence/Workers/OcrWorkerService.cs` | Enqueue ExtractionJob after successful OCR completion |

---

## External References

- Azure OpenAI API: https://learn.microsoft.com/en-us/azure/ai-services/openai/reference
- LiteLLM proxy documentation: https://docs.litellm.ai/docs/
- Polly circuit-breaker: https://github.com/App-vNext/Polly/wiki/Circuit-Breaker
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/dotnet/
- System.Text.Json schema validation: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/
- pgvector .NET: https://github.com/pgvector/pgvector-dotnet
- FR-CA-001: System MUST extract structured clinical entities from unstructured documents with confidence scores
- AIR-001: Hybrid extraction — model inference + deterministic normalization
- AIR-004: Source citation references for extracted facts
- AIR-005: Fallback to deterministic manual workflows when confidence below threshold
- AIR-008: Output schema validation ≥ 99% for extraction payloads
- AIR-009: Redact direct identifiers from prompts; log redaction actions
- AIR-010: Retrieval access control filters for patient-specific context
- TR-008: Provider-agnostic AI gateway with circuit-breaker fallback

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

- [ ] Unit tests pass for `PiiRedactionService` — SSN, phone, email, DOB redacted; redaction actions logged (AIR-009)
- [ ] Unit tests pass for `ExtractionPromptBuilder` — prompt constructed with system message, examples, chunked user text; token budget respected
- [ ] Unit tests pass for `ExtractionSchemaValidator` — valid JSON passes, invalid JSON fails; pass rate metric emitted (AIR-008)
- [ ] Unit tests pass for `NormalizationService` — drug name, allergy, diagnosis normalization rules applied
- [ ] Unit tests pass for `ClinicalExtractionService` — full pipeline: chunk → redact → prompt → validate → normalize → persist
- [ ] Unit tests pass for low text quality detection — `LowInputQuality` flag returned, document flagged (Edge Case 1)
- [ ] Unit tests pass for low-confidence flagging — facts below 70% threshold have `NeedsReview = true` (AC-3, AIR-005)
- [ ] Unit tests pass for `AiGatewayClient` — circuit-breaker opens after 5 failures, fallback triggered (TR-008)
- [ ] Integration tests pass for `ExtractionWorkerService` — jobs consumed, facts persisted, status updated
- [ ] **[AI Tasks]** Prompt templates validated with test inputs (medications, allergies, diagnoses, findings)
- [ ] **[AI Tasks]** Guardrails tested — schema validation rejects malformed output
- [ ] **[AI Tasks]** Fallback logic tested — circuit-breaker open → empty extraction + manual review flag (AIR-005)
- [ ] **[AI Tasks]** Token budget enforcement verified — prompts stay within MaxTokens limit
- [ ] **[AI Tasks]** Audit logging verified — prompts/responses logged with PII redacted (AIR-009)
- [ ] Schema validation pass rate ≥ 99% verified over test dataset (AC-4, AIR-008)
- [ ] Each fact has `document_id` reference for source traceability (AC-2, AIR-004)

---

## Implementation Checklist

- [ ] Create `AiGatewayConfiguration` and `ExtractionConfiguration` options classes
- [ ] Create `IAiGatewayClient` / `AiGatewayClient` with Polly circuit-breaker, retry, and OpenTelemetry metrics (TR-008)
- [ ] Create `IPiiRedactionService` / `PiiRedactionService` with regex PII redaction and audit logging (AIR-009)
- [ ] Create `IPromptBuilder` / `ExtractionPromptBuilder` with system prompt, few-shot examples, and token budget control
- [ ] Create `IExtractionSchemaValidator` / `ExtractionSchemaValidator` with JSON schema validation and 99% pass rate tracking (AIR-008, AC-4)
- [ ] Create `INormalizationService` / `NormalizationService` for drug, allergy, and diagnosis normalization (AIR-001 hybrid)
- [ ] Create `IClinicalExtractionService` / `ClinicalExtractionService`: quality check → chunk → redact → prompt → validate → normalize → flag low-confidence → persist with source reference
- [ ] **[AI Tasks - MANDATORY]** Reference prompt templates from AI References table during implementation
- [ ] **[AI Tasks - MANDATORY]** Implement and test guardrails before marking task complete
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-001, AIR-004, AIR-005, AIR-008, AIR-009, AIR-010 requirements are met

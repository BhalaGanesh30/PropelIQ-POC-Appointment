---
task_id: task_002
user_story: us_041
epic: EP-006
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_041] Async OCR Processing and Status Tracking
- **Story Location**: [.propel/context/tasks/EP-006/us_041/us_041.md](.propel/context/tasks/EP-006/us_041/us_041.md)
- **Acceptance Criteria**:
  - AC-1: Given a clean document is successfully uploaded, When the file is persisted, Then an OCR processing job is queued and the document record is updated with status "Queued."
  - AC-2: Given an OCR job is queued, When the background worker processes it using Tesseract, Then the document status transitions through "Processing" → "Completed" and the extracted text is stored against the document record.
  - AC-3: Given OCR processing completes, When I check the document status, Then the status shows "Completed" and the extracted text is available within 2 minutes p95 for files up to 10 MB.
  - AC-4: Given an OCR job fails, When the failure is detected, Then the document status is updated to "Failed," the error is logged, and the job is retried up to 3 times with exponential backoff before moving to the dead-letter queue.
- **Edge Cases**:
  - Edge Case 1: Low text quality scanned image — OCR produces low-confidence extraction; document flagged for manual review with raw OCR output stored.
  - Edge Case 2: Concurrent OCR jobs — queue workers process in parallel up to configured concurrency limit; back-pressure applied when queue depth exceeds threshold.

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
| Object Storage | Cloudflare R2 (S3-compatible) | N/A |
| Storage SDK | AWSSDK.S3 | latest |
| OCR Engine | Tesseract (via Tesseract.NET SDK) | 5.x |
| Queue | System.Threading.Channels | 8.x |
| Observability | OpenTelemetry | latest |
| Frontend | N/A | N/A |
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

> **Note**: FR-DM-002 is tagged [AI-CANDIDATE] and AIR-001 references OCR-assisted extraction. However, US_041 scope is limited to Tesseract-based OCR text extraction (deterministic). The AI-powered clinical entity extraction (medications, allergies, diagnoses) is a downstream concern (likely US_043+) that consumes the `extracted_text` produced by this task.

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

Implement an asynchronous OCR processing worker within the `ClinicalIntelligence` module that processes uploaded clinical documents using Tesseract OCR. The worker consumes jobs from an in-process `Channel<OcrJob>` queue, downloads the document from Cloudflare R2, executes OCR via the Tesseract.NET SDK, stores the extracted text in the `clinical_documents.extracted_text` column, and transitions the `extraction_status` through `Queued` → `Processing` → `Completed` (or `Failed`). The system enforces a 2-minute p95 processing SLA for files up to 10 MB (NFR-003). Failed jobs are retried up to 3 times with exponential backoff (1s, 4s, 16s) before being moved to a dead-letter queue table for manual investigation (TR-005, AC-4). Low-confidence extractions (average character confidence below a configurable threshold, default 60%) are flagged with `needs_manual_review = true` (Edge Case 1). Concurrency is limited to a configurable worker count (default 4) using `Channel` bounded capacity for back-pressure (Edge Case 2). The upload flow (US_040/task_002) is extended to enqueue an OCR job after a clean malware scan. A retry endpoint `POST /api/v1/documents/{id}/retry-ocr` allows manual re-triggering of failed OCR jobs.

---

## Dependent Tasks

- **us_040/task_002** — `DocumentUploadService`, `IR2StorageService`, `IClinicalDocumentRepository`, and `DocumentsController` must exist. This task extends the upload flow to enqueue OCR jobs.
- **us_040/task_003** — `clinical_documents` table with `extraction_status` enum and `extracted_text` column must be migrated.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `OcrJob` | CREATE | Record class: `Guid DocumentId`, `string R2ObjectKey`, `int RetryCount` |
| `OcrJobChannel` | CREATE | Singleton wrapper around `Channel<OcrJob>` with bounded capacity and back-pressure |
| `IOcrProcessingService` | CREATE | Interface: `ProcessDocumentAsync(OcrJob, CancellationToken)` |
| `TesseractOcrService` | CREATE | Tesseract.NET SDK implementation: download from R2, OCR, return extracted text with confidence |
| `OcrWorkerService` | CREATE | `BackgroundService` consuming from `OcrJobChannel`, processing with retry/dead-letter |
| `OcrConfiguration` | CREATE | Options class: `MaxRetries`, `ConcurrencyLimit`, `ConfidenceThreshold`, `TessdataPath` |
| `OcrProcessingResult` | CREATE | Record: `string ExtractedText`, `double AverageConfidence`, `bool NeedsManualReview` |
| `DeadLetterEntry` | CREATE | EF entity for `ocr_dead_letter_queue` table |
| `IDeadLetterRepository` | CREATE | Repository interface for dead-letter queue CRUD |
| `DeadLetterRepository` | CREATE | EF Core implementation |
| `DocumentUploadService` | MODIFY | After clean scan and R2 upload, enqueue `OcrJob` to `OcrJobChannel` and set `extraction_status = Queued` |
| `DocumentsController` | MODIFY | Add `POST {id}/retry-ocr` endpoint |
| `IClinicalDocumentRepository` | MODIFY | Add `GetFailedDocumentsAsync()`, `UpdateExtractionStatusAsync()` methods |
| `ClinicalDocumentRepository` | MODIFY | Implement new repository methods |
| `ClinicalDocument` (EF entity) | MODIFY | Add `NeedsManualReview` (bool) property |
| `ClinicalIntelligenceDbContext` | MODIFY | Add `DbSet<DeadLetterEntry>`, configure `ocr_dead_letter_queue` table, add migration for `needs_manual_review` column |
| `ClinicalIntelligenceModule` DI | MODIFY | Register `OcrJobChannel` (Singleton), `IOcrProcessingService` → `TesseractOcrService` (Scoped), `OcrWorkerService` (Hosted), `IDeadLetterRepository`, `OcrConfiguration` |

---

## Implementation Plan

1. **Create `OcrConfiguration`** options class in `ClinicalIntelligence/Configuration/OcrConfiguration.cs`: `MaxRetries` (int, default `3`), `ConcurrencyLimit` (int, default `4`), `ConfidenceThreshold` (double, default `0.60`), `TessdataPath` (string, default `"./tessdata"`), `BackoffBaseSeconds` (int, default `1`). Bind from `appsettings.json` section `Ocr`.
2. **Create `OcrJob` record** in `ClinicalIntelligence/Models/OcrJob.cs`: `Guid DocumentId`, `string R2ObjectKey`, `int RetryCount = 0`.
3. **Create `OcrJobChannel`** singleton in `ClinicalIntelligence/Queues/OcrJobChannel.cs`: wraps `Channel.CreateBounded<OcrJob>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait })` where `capacity = ConcurrencyLimit * 10`. Expose `Writer` and `Reader` properties. This provides in-process queueing with back-pressure when the queue depth exceeds the threshold (Edge Case 2).
4. **Create `OcrProcessingResult` record** in `ClinicalIntelligence/Models/OcrProcessingResult.cs`: `string ExtractedText`, `double AverageConfidence`, `bool NeedsManualReview`.
5. **Create `IOcrProcessingService` and `TesseractOcrService`**: Interface with `Task<OcrProcessingResult> ProcessDocumentAsync(OcrJob job, CancellationToken ct)`. Implementation: (a) download file from R2 via `IR2StorageService.DownloadDocumentAsync(job.R2ObjectKey)` to a temporary `MemoryStream`; (b) initialize Tesseract engine with `TessdataPath` and language `"eng"`; (c) execute OCR on the file stream; (d) extract text and per-page character confidence scores; (e) compute average confidence; (f) if `averageConfidence < OcrConfiguration.ConfidenceThreshold`, set `NeedsManualReview = true` (Edge Case 1); (g) return `OcrProcessingResult`.
6. **Create `OcrWorkerService`** as a `BackgroundService` in `ClinicalIntelligence/Workers/OcrWorkerService.cs`:
   - In `ExecuteAsync`, spawn `OcrConfiguration.ConcurrencyLimit` concurrent consumer tasks (using `Task.WhenAll`).
   - Each consumer reads from `OcrJobChannel.Reader` via `ReadAllAsync(ct)`.
   - For each `OcrJob`: (a) update `extraction_status = Processing` in DB; (b) call `IOcrProcessingService.ProcessDocumentAsync()`; (c) on success, update `extraction_status = Completed`, store `extracted_text` and `needs_manual_review` flag; (d) on exception, increment `RetryCount` — if `RetryCount < MaxRetries`, compute backoff delay `BaseSeconds * 4^RetryCount` seconds, re-enqueue to `OcrJobChannel.Writer` after delay; if `RetryCount >= MaxRetries`, update `extraction_status = Failed`, log error via `ILogger`, and write to `ocr_dead_letter_queue` table (AC-4).
   - Use `IServiceScopeFactory` for scoped services (EF context, repository).
   - Emit OpenTelemetry metrics: `ocr.jobs.processed`, `ocr.jobs.failed`, `ocr.processing.duration_ms`.
7. **Create `DeadLetterEntry` EF entity** and repository: `Id` (Guid PK), `DocumentId` (Guid FK), `ErrorMessage` (string), `StackTrace` (string, nullable), `RetryCount` (int), `CreatedAt` (DateTime). Table `ocr_dead_letter_queue`. `IDeadLetterRepository` with `AddAsync(DeadLetterEntry)` and `GetAllAsync()`.
8. **Add EF Core migration**: Add `needs_manual_review` (bool, default false) column to `clinical_documents` table. Create `ocr_dead_letter_queue` table.
9. **Modify `DocumentUploadService`**: After a clean malware scan and R2 upload in `UploadDocumentAsync()`, set `extraction_status = Queued` on the `ClinicalDocument` entity and enqueue `new OcrJob(document.DocumentId, document.R2ObjectKey)` to `OcrJobChannel.Writer.WriteAsync()`.
10. **Add retry endpoint to `DocumentsController`**: `[HttpPost("{id}/retry-ocr")] [Authorize(Roles = "Clinician,Staff")]` — validate document exists and `extraction_status == Failed`, reset `extraction_status = Queued`, re-enqueue `OcrJob` with `RetryCount = 0`, return `202 Accepted`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Configuration/
│   │   │   ├── ClamAvConfiguration.cs                ← EXISTS (US_040)
│   │   │   └── OcrConfiguration.cs                   ← CREATE
│   │   ├── Controllers/
│   │   │   └── DocumentsController.cs                ← MODIFY (add retry-ocr endpoint)
│   │   ├── DTOs/
│   │   │   └── [existing DTOs from US_040...]
│   │   ├── Entities/
│   │   │   ├── ClinicalDocument.cs                   ← MODIFY (add NeedsManualReview)
│   │   │   └── DeadLetterEntry.cs                    ← CREATE
│   │   ├── Models/
│   │   │   ├── OcrJob.cs                             ← CREATE
│   │   │   └── OcrProcessingResult.cs                ← CREATE
│   │   ├── Queues/
│   │   │   └── OcrJobChannel.cs                      ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalDocumentRepository.cs        ← MODIFY (add new methods)
│   │   │   ├── ClinicalDocumentRepository.cs         ← MODIFY (implement new methods)
│   │   │   ├── IDeadLetterRepository.cs              ← CREATE
│   │   │   └── DeadLetterRepository.cs               ← CREATE
│   │   ├── Services/
│   │   │   ├── IDocumentUploadService.cs             ← EXISTS (US_040)
│   │   │   ├── DocumentUploadService.cs              ← MODIFY (enqueue OCR job after clean scan)
│   │   │   ├── IOcrProcessingService.cs              ← CREATE
│   │   │   └── TesseractOcrService.cs                ← CREATE
│   │   ├── Workers/
│   │   │   ├── MalwareScanRetryService.cs            ← EXISTS (US_040)
│   │   │   └── OcrWorkerService.cs                   ← CREATE
│   │   └── Data/
│   │       ├── ClinicalIntelligenceDbContext.cs      ← MODIFY (add DeadLetterEntry, migration)
│   │       └── Migrations/
│   │           └── YYYYMMDDHHMMSS_AddOcrSupport.cs   ← CREATE
│   └── SharedServices/
│       └── Storage/
│           └── IR2StorageService.cs                  ← MODIFY (add DownloadDocumentAsync)
└── [existing project structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Configuration/OcrConfiguration.cs` | Options: MaxRetries, ConcurrencyLimit, ConfidenceThreshold, TessdataPath |
| CREATE | `Modules/ClinicalIntelligence/Models/OcrJob.cs` | Record: DocumentId, R2ObjectKey, RetryCount |
| CREATE | `Modules/ClinicalIntelligence/Models/OcrProcessingResult.cs` | Record: ExtractedText, AverageConfidence, NeedsManualReview |
| CREATE | `Modules/ClinicalIntelligence/Queues/OcrJobChannel.cs` | Bounded Channel wrapper with back-pressure |
| CREATE | `Modules/ClinicalIntelligence/Services/IOcrProcessingService.cs` | OCR processing interface |
| CREATE | `Modules/ClinicalIntelligence/Services/TesseractOcrService.cs` | Tesseract.NET SDK implementation: download, OCR, confidence scoring |
| CREATE | `Modules/ClinicalIntelligence/Workers/OcrWorkerService.cs` | BackgroundService: concurrent consumers, retry with exponential backoff, dead-letter |
| CREATE | `Modules/ClinicalIntelligence/Entities/DeadLetterEntry.cs` | EF entity for `ocr_dead_letter_queue` table |
| CREATE | `Modules/ClinicalIntelligence/Repositories/IDeadLetterRepository.cs` | Dead-letter queue repository interface |
| CREATE | `Modules/ClinicalIntelligence/Repositories/DeadLetterRepository.cs` | EF Core implementation |
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddOcrSupport.cs` | Migration: `needs_manual_review` column, `ocr_dead_letter_queue` table |
| MODIFY | `Modules/ClinicalIntelligence/Controllers/DocumentsController.cs` | Add `POST {id}/retry-ocr` endpoint with `[Authorize(Roles = "Clinician,Staff")]` |
| MODIFY | `Modules/ClinicalIntelligence/Services/DocumentUploadService.cs` | Enqueue OcrJob after clean scan, set `extraction_status = Queued` |
| MODIFY | `Modules/ClinicalIntelligence/Entities/ClinicalDocument.cs` | Add `NeedsManualReview` bool property |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalDocumentRepository.cs` | Add `GetFailedDocumentsAsync()`, `UpdateExtractionStatusAsync()` |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalDocumentRepository.cs` | Implement new methods |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Add `DbSet<DeadLetterEntry>`, configure entity |
| MODIFY | `Modules/SharedServices/Storage/IR2StorageService.cs` | Add `DownloadDocumentAsync(string objectKey)` method |

---

## External References

- Tesseract.NET SDK (Tesseract OCR for .NET): https://github.com/charlesw/tesseract
- System.Threading.Channels: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
- BackgroundService in ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
- Exponential backoff pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/retry
- OpenTelemetry .NET metrics: https://opentelemetry.io/docs/languages/dotnet/instrumentation/#metrics
- FR-DM-002: System MUST process uploaded documents with OCR and extraction tracking with completion target under 2 minutes
- NFR-003: System MUST complete OCR and document extraction processing within 2 minutes p95 for files up to 10 MB
- TR-005: System MUST use asynchronous worker processing for OCR and extraction jobs with retry policies and dead-letter handling
- AIR-001: System MUST perform OCR-assisted extraction (downstream consumer of this task's output)

---

## Build Commands

```bash
# Restore packages (including Tesseract.NET SDK)
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

- [ ] Unit tests pass for `TesseractOcrService` — mock R2 download, verify OCR execution and confidence scoring
- [ ] Unit tests pass for `OcrWorkerService` — verify status transitions (Queued → Processing → Completed), retry logic (3 retries with exponential backoff), dead-letter on exhausted retries (AC-4)
- [ ] Unit tests pass for `OcrJobChannel` — bounded capacity enforced, back-pressure applied at limit (Edge Case 2)
- [ ] Unit tests pass for low-confidence detection — average confidence below threshold flags `NeedsManualReview` (Edge Case 1)
- [ ] Integration tests pass for `DocumentsController` retry endpoint — `POST {id}/retry-ocr` returns 202, resets status to Queued
- [ ] Processing completes within 2 minutes p95 for a 10 MB PDF (NFR-003) — verified via integration test with timing assertion
- [ ] Dead-letter queue entry created after 3 failed retries with error details (AC-4)
- [ ] `extraction_status` transitions are correct: Queued → Processing → Completed (happy path), Queued → Processing → Failed (error path)
- [ ] `[Authorize(Roles = "Clinician,Staff")]` applied to retry endpoint — unauthorized users receive 403
- [ ] OpenTelemetry metrics emitted: `ocr.jobs.processed`, `ocr.jobs.failed`, `ocr.processing.duration_ms`

---

## Implementation Checklist

- [X] Create `OcrConfiguration` options class bound to `appsettings.json` section `Ocr`
- [X] Create `OcrJob` record and `OcrProcessingResult` record in Models
- [X] Create `OcrJobChannel` singleton with bounded capacity and back-pressure (Edge Case 2)
- [X] Create `IOcrProcessingService` / `TesseractOcrService`: R2 download → Tesseract OCR → confidence scoring → manual review flag (Edge Case 1)
- [X] Create `OcrWorkerService` (`BackgroundService`): concurrent consumers, exponential backoff retry (1s, 4s, 16s), dead-letter after 3 failures (AC-4, TR-005)
- [X] Create `DeadLetterEntry` entity, repository, and `ocr_dead_letter_queue` table via EF migration
- [X] Modify `DocumentUploadService` to enqueue OCR job after clean malware scan (AC-1)
- [X] Add `POST {id}/retry-ocr` endpoint to `DocumentsController` with `[Authorize(Roles = "Clinician,Staff")]`
- [X] Register all services in DI: `OcrJobChannel` (Singleton), `TesseractOcrService` (Scoped), `OcrWorkerService` (Hosted), repositories
- [X] Emit OpenTelemetry metrics for OCR job processing

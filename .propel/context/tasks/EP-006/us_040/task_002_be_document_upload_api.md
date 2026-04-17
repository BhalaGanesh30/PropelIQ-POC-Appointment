---
task_id: task_002
user_story: us_040
epic: EP-006
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_040] Document Upload with Malware Scanning
- **Story Location**: [.propel/context/tasks/EP-006/us_040/us_040.md](.propel/context/tasks/EP-006/us_040/us_040.md)
- **Acceptance Criteria**:
  - AC-1: Given I am authenticated, When I select and submit a file for upload, Then the system validates the file type (PDF, JPG, PNG, TIFF) and size (max 10 MB) before accepting the file.
  - AC-2: Given a valid file is submitted, When the malware scan is executed, Then the scan completes before the file is persisted and a clean file is stored in encrypted cloud storage.
  - AC-3: Given the malware scan detects a threat, When the scan result is returned, Then the file is rejected, not persisted, the upload response returns an error message, and the event is logged.
  - AC-4: Given a file type not in the approved list is submitted, When the type validation runs, Then the API returns HTTP 400 with a message listing the accepted file types.
- **Edge Cases**:
  - Edge Case 1: Malware scanner unavailable — upload is queued in a pending scan state; file is not accessible until scan completes; `scan_result` is set to `PendingScan`.
  - Edge Case 2: File exceeds 10 MB — HTTP 400 returned immediately; no partial upload persisted.

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

> **Note**: US_040 references SCR-022 (Compliance Reports, EP-010). The document upload screen is SCR-011 per figma_spec.md, which is the dedicated Document Upload screen under EP-006.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
| Object Storage | Cloudflare R2 (S3-compatible) | N/A |
| Storage SDK | AWSSDK.S3 | latest |
| Malware Scanning | nClam (ClamAV .NET client) | latest |
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

Implement the document upload API with malware scanning within the `ClinicalIntelligence` module. The API consists of two endpoints: `POST /api/v1/documents/upload` for file upload with validation and malware scanning, and `GET /api/v1/documents/{id}/status` for polling the scan and processing status. The upload endpoint accepts multipart/form-data with a single file and `patientId`. Server-side validation enforces file type via magic-byte inspection (PDF `%PDF`, JPEG `FF D8 FF`, PNG `89 50 4E 47`, TIFF `49 49 2A 00` or `4D 4D 00 2A`) and file size (max 10 MB, enforced via ASP.NET Core `RequestSizeLimit` attribute). Valid files are streamed to a temporary buffer for ClamAV malware scanning via the `nClam` NuGet package. Clean files (AC-2) are uploaded to Cloudflare R2 with SSE-S3 encryption (reusing `R2Configuration` and `IR2StorageService` patterns from US_038/task_002). Threat-detected files (AC-3) are rejected without persistence, the scan event is logged to the `security_events` audit trail via `ILogger`, and an HTTP 400 response is returned. If ClamAV is unreachable (Edge Case 1), the file is stored in a quarantine R2 prefix (`quarantine/`), `scan_result` is set to `PendingScan`, and a background retry service (`MalwareScanRetryService`) re-scans quarantined files when the scanner recovers. The `clinical_documents` record is persisted with the R2 object key, scan result, content type, file size, and original filename. The status endpoint returns the current `scan_result` and `extraction_status` values for polling by the frontend.

---

## Dependent Tasks

- **us_040/task_003** — `clinical_documents` table with `scan_result` and `extraction_status` enums, `r2_object_key`, `file_size_bytes`, `content_type`, `original_filename` columns must be migrated.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentsController` | CREATE | `[ApiController]` at `api/v1/documents` — upload and status endpoints |
| `IDocumentUploadService` | CREATE | Interface: `UploadDocumentAsync`, `GetDocumentStatusAsync` |
| `DocumentUploadService` | CREATE | Implementation: file validation, malware scanning, R2 upload, DB persistence |
| `IMalwareScanService` | CREATE | Interface wrapping ClamAV scanning via nClam |
| `ClamAvScanService` | CREATE | nClam-based ClamAV client; configurable host/port via `ClamAvConfiguration` |
| `ClamAvConfiguration` | CREATE | Options class: `Host`, `Port`, `TimeoutSeconds` |
| `MalwareScanRetryService` | CREATE | `BackgroundService` — periodically re-scans files in `PendingScan` state |
| `DocumentUploadRequest` | CREATE | DTO: `IFormFile File`, `Guid PatientId` |
| `DocumentUploadResponse` | CREATE | DTO: `Guid DocumentId`, `string ScanResult`, `string Message` |
| `DocumentStatusResponse` | CREATE | DTO: `Guid DocumentId`, `string ScanResult`, `string ExtractionStatus` |
| `FileTypeValidator` | CREATE | Static utility: magic-byte validation for PDF/JPEG/PNG/TIFF |
| `IR2StorageService` | MODIFY | Add `UploadDocumentAsync` method (reuses US_038 R2 pattern) |
| `ClinicalDocument` (EF entity) | CREATE | EF Core entity mapping to `clinical_documents` table |
| `IClinicalDocumentRepository` | CREATE | Repository interface for `clinical_documents` CRUD |
| `ClinicalDocumentRepository` | CREATE | EF Core repository implementation |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new services: `IDocumentUploadService`, `IMalwareScanService`, repository |
| `Program.cs` or module bootstrap | MODIFY | Register `ClamAvConfiguration` from `appsettings.json`, register `MalwareScanRetryService` |

---

## Implementation Plan

1. **Create `ClamAvConfiguration`** options class in `ClinicalIntelligence/Configuration/ClamAvConfiguration.cs`: properties `Host` (string, default `"localhost"`), `Port` (int, default `3310`), `TimeoutSeconds` (int, default `30`). Bind from `appsettings.json` section `ClamAv`.
2. **Create `IMalwareScanService` and `ClamAvScanService`**: Interface with `Task<ScanResult> ScanAsync(Stream fileStream, CancellationToken ct)` returning `ScanResult { Clean, ThreatDetected, ScannerUnavailable }` enum. Implementation uses `nClam.ClamClient` to connect to ClamAV daemon. On `SocketException` or timeout, return `ScannerUnavailable`. Log threat details via `ILogger<ClamAvScanService>` when a threat is detected (AC-3 logging requirement).
3. **Create `FileTypeValidator`** static class in `ClinicalIntelligence/Validators/FileTypeValidator.cs`: method `bool IsAllowedFileType(Stream fileStream)` reads first 8 bytes and checks against magic-byte signatures for PDF (`25 50 44 46`), JPEG (`FF D8 FF`), PNG (`89 50 4E 47 0D 0A 1A 0A`), TIFF (`49 49 2A 00` or `4D 4D 00 2A`). Returns `false` for unrecognized signatures. Resets stream position after read.
4. **Create `DocumentUploadRequest`** DTO with `[Required] IFormFile File` and `[Required] Guid PatientId`. Create `DocumentUploadResponse` DTO with `Guid DocumentId`, `string ScanResult`, `string Message`. Create `DocumentStatusResponse` with `Guid DocumentId`, `string ScanResult`, `string ExtractionStatus`.
5. **Create `ClinicalDocument` EF entity** mapping to `clinical_documents` table: `DocumentId` (Guid PK), `PatientId` (Guid FK), `R2ObjectKey` (string), `OriginalFilename` (string), `ContentType` (string), `FileSizeBytes` (long), `Category` (enum), `ScanResult` (enum: `Clean`, `ThreatDetected`, `PendingScan`), `ExtractionStatus` (enum: `Queued`, `Processing`, `Completed`, `Failed`), `ExtractedText` (string, nullable), `UploadedAt` (DateTime). Configure in `ClinicalIntelligenceDbContext`.
6. **Create `IClinicalDocumentRepository` and `ClinicalDocumentRepository`**: methods `AddAsync(ClinicalDocument)`, `GetByIdAsync(Guid)`, `GetPendingScanDocumentsAsync()`, `UpdateAsync(ClinicalDocument)`. EF Core implementation injecting `ClinicalIntelligenceDbContext`.
7. **Create `IDocumentUploadService` and `DocumentUploadService`**: `UploadDocumentAsync(DocumentUploadRequest, CancellationToken)` orchestrates: (a) validate file type via `FileTypeValidator.IsAllowedFileType()` — reject with `ValidationException` listing accepted types if invalid (AC-4); (b) validate file size <= 10 MB — reject with `ValidationException` if exceeded (Edge Case 2); (c) scan via `IMalwareScanService.ScanAsync()` — if `ThreatDetected`, log security event and throw `SecurityException` (AC-3); if `ScannerUnavailable`, set `scanResult = PendingScan` and upload to `quarantine/` prefix (Edge Case 1); if `Clean`, upload to `documents/{patientId}/{documentId}` prefix; (d) persist `ClinicalDocument` record via repository; (e) return `DocumentUploadResponse`. `GetDocumentStatusAsync(Guid)` returns `DocumentStatusResponse` from repository.
8. **Create `DocumentsController`** at `api/v1/documents` with `[Authorize(Roles = "Patient,Staff")]`:
   - `[HttpPost("upload")] [RequestSizeLimit(10_485_760)]` accepting `[FromForm] DocumentUploadRequest`. Returns `201 Created` with `DocumentUploadResponse` on success, `400 BadRequest` for validation failures and threats, `503 ServiceUnavailable` header hint if scanner unavailable (file still accepted in PendingScan).
   - `[HttpGet("{id}/status")]` returning `DocumentStatusResponse`.
9. **Create `MalwareScanRetryService`** as a `BackgroundService`: every 60 seconds, query `GetPendingScanDocumentsAsync()`, for each file download from `quarantine/` R2 prefix, re-scan via `IMalwareScanService.ScanAsync()`. If `Clean`, move to `documents/` prefix and update `scan_result` to `Clean`. If `ThreatDetected`, delete from R2 and update `scan_result` to `ThreatDetected`. If still `ScannerUnavailable`, skip and retry next cycle. Use `IServiceScopeFactory` for scoped services.
10. **Register services**: In `ClinicalIntelligenceModule` DI setup: register `IMalwareScanService` → `ClamAvScanService` (Scoped), `IDocumentUploadService` → `DocumentUploadService` (Scoped), `IClinicalDocumentRepository` → `ClinicalDocumentRepository` (Scoped). Register `MalwareScanRetryService` as hosted service. Bind `ClamAvConfiguration` from config.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Configuration/
│   │   │   └── ClamAvConfiguration.cs                ← CREATE
│   │   ├── Controllers/
│   │   │   └── DocumentsController.cs                ← CREATE
│   │   ├── DTOs/
│   │   │   ├── DocumentUploadRequest.cs              ← CREATE
│   │   │   ├── DocumentUploadResponse.cs             ← CREATE
│   │   │   └── DocumentStatusResponse.cs             ← CREATE
│   │   ├── Entities/
│   │   │   └── ClinicalDocument.cs                   ← CREATE
│   │   ├── Enums/
│   │   │   ├── ScanResult.cs                         ← CREATE
│   │   │   └── ExtractionStatus.cs                   ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalDocumentRepository.cs        ← CREATE
│   │   │   └── ClinicalDocumentRepository.cs         ← CREATE
│   │   ├── Services/
│   │   │   ├── IDocumentUploadService.cs             ← CREATE
│   │   │   ├── DocumentUploadService.cs              ← CREATE
│   │   │   ├── IMalwareScanService.cs                ← CREATE
│   │   │   ├── ClamAvScanService.cs                  ← CREATE
│   │   │   └── MalwareScanRetryService.cs            ← CREATE
│   │   └── Validators/
│   │       └── FileTypeValidator.cs                  ← CREATE
│   └── SharedServices/
│       └── Storage/
│           └── IR2StorageService.cs                  ← MODIFY (add UploadDocumentAsync)
└── [existing project structure...]
```

> Placeholder: Update this tree after task_003 migration is applied and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Configuration/ClamAvConfiguration.cs` | Options class for ClamAV daemon connection settings |
| CREATE | `Modules/ClinicalIntelligence/Controllers/DocumentsController.cs` | API controller: `POST upload`, `GET {id}/status`, `[Authorize(Roles = "Patient,Staff")]` |
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentUploadRequest.cs` | Upload request DTO with `IFormFile` and `PatientId` |
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentUploadResponse.cs` | Upload response DTO with `DocumentId`, `ScanResult`, `Message` |
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentStatusResponse.cs` | Status polling response DTO |
| CREATE | `Modules/ClinicalIntelligence/Entities/ClinicalDocument.cs` | EF Core entity mapping to `clinical_documents` table |
| CREATE | `Modules/ClinicalIntelligence/Enums/ScanResult.cs` | Enum: `Clean`, `ThreatDetected`, `PendingScan` |
| CREATE | `Modules/ClinicalIntelligence/Enums/ExtractionStatus.cs` | Enum: `Queued`, `Processing`, `Completed`, `Failed` |
| CREATE | `Modules/ClinicalIntelligence/Repositories/IClinicalDocumentRepository.cs` | Repository interface for `clinical_documents` |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ClinicalDocumentRepository.cs` | EF Core repository implementation |
| CREATE | `Modules/ClinicalIntelligence/Services/IDocumentUploadService.cs` | Service interface: upload orchestration and status query |
| CREATE | `Modules/ClinicalIntelligence/Services/DocumentUploadService.cs` | Upload orchestration: validate → scan → store → persist |
| CREATE | `Modules/ClinicalIntelligence/Services/IMalwareScanService.cs` | Malware scan interface |
| CREATE | `Modules/ClinicalIntelligence/Services/ClamAvScanService.cs` | nClam ClamAV client implementation |
| CREATE | `Modules/ClinicalIntelligence/Services/MalwareScanRetryService.cs` | `BackgroundService` retrying `PendingScan` files |
| CREATE | `Modules/ClinicalIntelligence/Validators/FileTypeValidator.cs` | Magic-byte file type validation for PDF/JPEG/PNG/TIFF |
| MODIFY | `Modules/SharedServices/Storage/IR2StorageService.cs` | Add `UploadDocumentAsync` method |
| MODIFY | `Program.cs` or module bootstrap | Register `ClamAvConfiguration`, `MalwareScanRetryService`, new DI services |

---

## External References

- nClam NuGet package (ClamAV .NET client): https://github.com/tekmaven/nClam
- ClamAV daemon documentation: https://docs.clamav.net/
- AWSSDK.S3 for Cloudflare R2: https://docs.aws.amazon.com/sdkfornet/v3/apidocs/
- ASP.NET Core file upload: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads
- `RequestSizeLimitAttribute`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.requestsizelimitattribute
- FR-DM-001: System MUST accept PDF, JPG, PNG, and TIFF files up to 10 MB and complete malware scan before persistence
- TR-005: Asynchronous worker processing for OCR and extraction jobs with retry policies and dead-letter handling
- EP-006: Document upload API with malware scanning before persistence, encrypted storage in Cloudflare R2

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

- [ ] Unit tests pass for `FileTypeValidator` — PDF/JPEG/PNG/TIFF accepted, EXE/ZIP/unknown rejected
- [ ] Unit tests pass for `DocumentUploadService` — happy path (clean scan → R2 upload → DB persist), threat detected (reject, no persist), scanner unavailable (quarantine, PendingScan)
- [ ] Unit tests pass for `ClamAvScanService` — mock `ClamClient` for clean, infected, and unreachable scenarios
- [ ] Unit tests pass for `MalwareScanRetryService` — re-scan PendingScan files, move clean files, delete threats
- [ ] Integration tests pass for `DocumentsController` — upload returns 201, invalid type returns 400 (AC-4), oversized returns 400 (Edge Case 2), threat returns 400 (AC-3)
- [ ] Malware scan completes before file persistence (AC-2) — verified via test asserting no R2 write before scan result
- [ ] `[RequestSizeLimit(10_485_760)]` enforced — oversized requests return 413 before reaching controller logic
- [ ] `[Authorize(Roles = "Patient,Staff")]` applied — unauthorized users receive 403
- [ ] Security event logged when threat detected (AC-3) — verified via `ILogger` mock assertion

---

## Implementation Checklist

- [ ] Create `ClamAvConfiguration` options class bound to `appsettings.json` section `ClamAv`
- [ ] Create `IMalwareScanService` interface and `ClamAvScanService` implementation using nClam; handle `SocketException` → `ScannerUnavailable`
- [ ] Create `FileTypeValidator` with magic-byte checks for PDF, JPEG, PNG, TIFF; reject all other types (AC-4)
- [ ] Create DTOs: `DocumentUploadRequest` (`IFormFile`, `PatientId`), `DocumentUploadResponse`, `DocumentStatusResponse`
- [ ] Create `ClinicalDocument` EF entity and configure in `ClinicalIntelligenceDbContext`
- [ ] Create `IClinicalDocumentRepository` / `ClinicalDocumentRepository` with `AddAsync`, `GetByIdAsync`, `GetPendingScanDocumentsAsync`, `UpdateAsync`
- [ ] Create `IDocumentUploadService` / `DocumentUploadService`: validate → scan → store (R2) → persist (DB) → respond
- [ ] Create `DocumentsController` with `POST upload` (201/400) and `GET {id}/status`; apply `[Authorize]` and `[RequestSizeLimit]`
- [ ] Create `MalwareScanRetryService` (`BackgroundService`) — re-scan `PendingScan` files every 60 seconds; move clean, delete threats
- [ ] Register all services in DI container and bind `ClamAvConfiguration`

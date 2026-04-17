---
task_id: task_002
user_story: us_042
epic: EP-006
layer: Backend
status: not-started
effort_hours: 6
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_042] In-Browser Document Viewer
- **Story Location**: [.propel/context/tasks/EP-006/us_042/us_042.md](.propel/context/tasks/EP-006/us_042/us_042.md)
- **Acceptance Criteria**:
  - AC-1: Given I navigate to a patient's documents, When I click on a completed document, Then the document renders in an embedded browser viewer within 3 seconds.
  - AC-2: Given the document viewer is open, When I use the zoom controls (in/out), Then the document scales smoothly between 50% and 200% zoom levels.
  - AC-3: Given the document viewer is open, When I use the rotate control, Then the document rotates 90 degrees clockwise per click.
  - AC-4: Given OCR extraction is available for the document, When I type a search term in the full-text search field, Then matching text occurrences are highlighted in the document and I can navigate between them using next/previous controls.
- **Edge Cases**:
  - Edge Case 1: Document has no OCR text available (still processing) — API returns `extractionStatus` field so frontend can disable search.
  - Edge Case 2: Large multi-page documents — pre-signed URL supports range requests enabling progressive page loading on the client.

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

> **Note**: US_042 references SCR-023 (KPI Dashboard, EP-011). The document viewer screen is SCR-013 per figma_spec.md, which is the dedicated Document Viewer screen under EP-006.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL with pg_trgm | 15.x |
| Object Storage | Cloudflare R2 (S3-compatible) | N/A |
| Storage SDK | AWSSDK.S3 | latest |
| Cache | Redis (StackExchange.Redis) | 2.x |
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

Implement two API endpoints within the `ClinicalIntelligence` module to support the in-browser document viewer: `GET /api/v1/documents/{id}/content` for serving document files via pre-signed Cloudflare R2 URLs, and `GET /api/v1/documents/{id}/search` for full-text search over OCR-extracted text. The content endpoint generates a short-lived pre-signed R2 URL (15-minute expiry) for the requested document, avoiding direct file streaming through the API server and enabling the frontend to load the document directly from R2 within the 3-second render target (AC-1). The search endpoint queries the `clinical_documents.extracted_text` column using PostgreSQL full-text search with `ts_vector`/`ts_query` for ranked results and `pg_trgm` trigram matching for fuzzy search. Results include matched text snippets, positions, and page references where available. The endpoint returns `extractionStatus` so the frontend can disable search when OCR is still processing (Edge Case 1). Both endpoints are authorized for Patient, Staff, and Clinician roles with patient-scoped access control ensuring users can only view documents belonging to their authorized patients.

---

## Dependent Tasks

- **us_040/task_002** — `DocumentsController`, `IR2StorageService`, `IClinicalDocumentRepository` must exist as the base this task extends.
- **us_040/task_003** — `clinical_documents` table with `extracted_text`, `r2_object_key`, `extraction_status` must be migrated.
- **us_041/task_002** — OCR worker must populate `extracted_text` for search to return results.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentsController` | MODIFY | Add `GET {id}/content` and `GET {id}/search` endpoints |
| `IDocumentViewerService` | CREATE | Interface: `GetDocumentContentAsync`, `SearchDocumentAsync` |
| `DocumentViewerService` | CREATE | Implementation: pre-signed URL generation, full-text search |
| `DocumentContentResponse` | CREATE | DTO: `PreSignedUrl`, `ContentType`, `ExtractionStatus`, `OriginalFilename`, `PageCount` |
| `DocumentSearchRequest` | CREATE | DTO: `string Term` (query param) |
| `DocumentSearchResponse` | CREATE | DTO: `Matches[]`, `TotalCount`, `ExtractionStatus` |
| `SearchMatchDto` | CREATE | DTO: `Text`, `PageNumber`, `Position`, `ContextBefore`, `ContextAfter` |
| `IR2StorageService` | MODIFY | Add `GeneratePreSignedUrlAsync(string objectKey, TimeSpan expiry)` method |
| `IClinicalDocumentRepository` | MODIFY | Add `SearchExtractedTextAsync(Guid documentId, string searchTerm)` method |
| `ClinicalDocumentRepository` | MODIFY | Implement full-text search with `ts_vector`/`ts_query` and trigram fallback |
| EF Core migration | CREATE | Add GIN index on `extracted_text` for full-text search performance |

---

## Implementation Plan

1. **Extend `IR2StorageService`** with `Task<string> GeneratePreSignedUrlAsync(string objectKey, TimeSpan expiry)`: use `AWSSDK.S3` `GetPreSignedUrlRequest` with the configured R2 bucket and the document's `r2_object_key`. Set expiry to 15 minutes. Return the pre-signed URL string. This avoids streaming files through the API server, enabling faster document loads (AC-1).
2. **Create `DocumentContentResponse` DTO** in `ClinicalIntelligence/DTOs/DocumentContentResponse.cs`: `string PreSignedUrl`, `string ContentType`, `string ExtractionStatus`, `string OriginalFilename`.
3. **Create `DocumentSearchResponse` and `SearchMatchDto` DTOs**: `DocumentSearchResponse` with `List<SearchMatchDto> Matches`, `int TotalCount`, `string ExtractionStatus`. `SearchMatchDto` with `string Text` (matched snippet), `int? PageNumber`, `int Position` (character offset), `string ContextBefore` (50 chars before match), `string ContextAfter` (50 chars after match).
4. **Create `IDocumentViewerService` and `DocumentViewerService`**:
   - `GetDocumentContentAsync(Guid documentId, CancellationToken ct)`: fetch `ClinicalDocument` by ID, validate it exists and `scan_result = Clean`, generate pre-signed URL via `IR2StorageService.GeneratePreSignedUrlAsync()`, return `DocumentContentResponse` with content type, extraction status, and filename.
   - `SearchDocumentAsync(Guid documentId, string searchTerm, CancellationToken ct)`: fetch document, check `extraction_status = Completed` — if not, return response with empty matches and current `extractionStatus` (Edge Case 1). If completed, call `IClinicalDocumentRepository.SearchExtractedTextAsync()` for full-text search. Return `DocumentSearchResponse` with match snippets, positions, and context.
5. **Implement `SearchExtractedTextAsync` in repository**: Execute a PostgreSQL query using `ts_headline()` for highlighted snippets and `ts_rank()` for relevance ranking:
   ```sql
   SELECT ts_headline('english', extracted_text, plainto_tsquery('english', @term),
          'StartSel=<mark>, StopSel=</mark>, MaxWords=20, MinWords=10') AS snippet,
          ts_rank(to_tsvector('english', extracted_text), plainto_tsquery('english', @term)) AS rank
   FROM clinical_documents
   WHERE document_id = @id
     AND to_tsvector('english', extracted_text) @@ plainto_tsquery('english', @term)
   ```
   For position-based matches, use `regexp_matches()` to find all occurrences and extract character offsets. Fall back to `pg_trgm` similarity search if `ts_query` returns no results (fuzzy matching for typos).
6. **Add EF Core migration**: Create a GIN index on `to_tsvector('english', extracted_text)` for full-text search performance:
   ```sql
   CREATE INDEX ix_clinical_documents_extracted_text_fts
       ON clinical_documents
       USING GIN (to_tsvector('english', extracted_text));
   ```
7. **Add endpoints to `DocumentsController`**:
   - `[HttpGet("{id}/content")] [Authorize(Roles = "Patient,Staff,Clinician")]`: validate document exists and belongs to the authorized patient, call `IDocumentViewerService.GetDocumentContentAsync()`, return `200 OK` with `DocumentContentResponse`. Return `404 NotFound` if document does not exist, `403 Forbidden` if patient mismatch.
   - `[HttpGet("{id}/search")] [Authorize(Roles = "Patient,Staff,Clinician")]`: accept `[FromQuery] string term` (minimum 2 characters, sanitized), call `IDocumentViewerService.SearchDocumentAsync()`, return `200 OK` with `DocumentSearchResponse`. Return `400 BadRequest` if term is empty or under 2 characters.
8. **Register services**: Register `IDocumentViewerService` → `DocumentViewerService` (Scoped) in `ClinicalIntelligenceModule` DI.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   └── DocumentsController.cs                ← MODIFY (add content + search endpoints)
│   │   ├── DTOs/
│   │   │   ├── DocumentContentResponse.cs            ← CREATE
│   │   │   ├── DocumentSearchResponse.cs             ← CREATE
│   │   │   ├── SearchMatchDto.cs                     ← CREATE
│   │   │   └── [existing DTOs from US_040/US_041...]
│   │   ├── Services/
│   │   │   ├── IDocumentViewerService.cs             ← CREATE
│   │   │   ├── DocumentViewerService.cs              ← CREATE
│   │   │   └── [existing services from US_040/US_041...]
│   │   ├── Repositories/
│   │   │   ├── IClinicalDocumentRepository.cs        ← MODIFY (add SearchExtractedTextAsync)
│   │   │   ├── ClinicalDocumentRepository.cs         ← MODIFY (implement FTS query)
│   │   │   └── [existing repositories...]
│   │   └── Data/
│   │       ├── ClinicalIntelligenceDbContext.cs      ← EXISTS
│   │       └── Migrations/
│   │           └── YYYYMMDDHHMMSS_AddFullTextSearchIndex.cs  ← CREATE
│   └── SharedServices/
│       └── Storage/
│           └── IR2StorageService.cs                  ← MODIFY (add GeneratePreSignedUrlAsync)
└── [existing project structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentContentResponse.cs` | DTO: PreSignedUrl, ContentType, ExtractionStatus, OriginalFilename |
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentSearchResponse.cs` | DTO: Matches list, TotalCount, ExtractionStatus |
| CREATE | `Modules/ClinicalIntelligence/DTOs/SearchMatchDto.cs` | DTO: Text, PageNumber, Position, ContextBefore, ContextAfter |
| CREATE | `Modules/ClinicalIntelligence/Services/IDocumentViewerService.cs` | Interface: GetDocumentContentAsync, SearchDocumentAsync |
| CREATE | `Modules/ClinicalIntelligence/Services/DocumentViewerService.cs` | Pre-signed URL generation, full-text search orchestration |
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddFullTextSearchIndex.cs` | GIN index on `to_tsvector('english', extracted_text)` |
| MODIFY | `Modules/ClinicalIntelligence/Controllers/DocumentsController.cs` | Add `GET {id}/content` and `GET {id}/search` endpoints with authorization |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalDocumentRepository.cs` | Add `SearchExtractedTextAsync()` method |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalDocumentRepository.cs` | Implement full-text search using `ts_vector`/`ts_query` + `pg_trgm` fallback |
| MODIFY | `Modules/SharedServices/Storage/IR2StorageService.cs` | Add `GeneratePreSignedUrlAsync()` for pre-signed R2 URLs |

---

## External References

- AWS SDK GetPreSignedUrl: https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/S3/MS3GetPreSignedURL.html
- PostgreSQL Full-Text Search: https://www.postgresql.org/docs/15/textsearch.html
- PostgreSQL `ts_headline`: https://www.postgresql.org/docs/15/textsearch-controls.html#TEXTSEARCH-HEADLINE
- PostgreSQL `pg_trgm` trigram matching: https://www.postgresql.org/docs/15/pgtrgm.html
- PostgreSQL GIN index: https://www.postgresql.org/docs/15/gin.html
- FR-DM-003: System MUST provide in-browser viewing with zoom, rotate, and full-text search over extracted content
- UXR-109: Document viewer MUST support zoom, rotate, and full-text search with keyboard shortcuts

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

# Generate EF Core migration
dotnet ef migrations add AddFullTextSearchIndex \
  --project src/Modules/ClinicalIntelligence \
  --startup-project src/Api

# Run the API
dotnet run --project src/Api/Api.csproj
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `DocumentViewerService.GetDocumentContentAsync()` — returns pre-signed URL for clean documents, 404 for missing
- [ ] Unit tests pass for `DocumentViewerService.SearchDocumentAsync()` — returns matches for completed OCR, empty for in-progress (Edge Case 1)
- [ ] Unit tests pass for `ClinicalDocumentRepository.SearchExtractedTextAsync()` — FTS query returns ranked matches with snippets
- [ ] Integration tests pass for `GET {id}/content` — returns 200 with pre-signed URL, 404 for missing, 403 for unauthorized patient
- [ ] Integration tests pass for `GET {id}/search` — returns matches, 400 for empty term, correct `extractionStatus`
- [ ] Pre-signed URL is valid for 15 minutes and correctly points to the R2 object
- [ ] GIN index exists on `to_tsvector('english', extracted_text)` — verified via `EXPLAIN ANALYZE`
- [ ] `[Authorize(Roles = "Patient,Staff,Clinician")]` applied — unauthorized users receive 403
- [ ] Patient-scoped access control enforced — users cannot view documents of unauthorized patients
- [ ] Search term is sanitized — no SQL injection via search input

---

## Implementation Checklist

- [ ] Extend `IR2StorageService` with `GeneratePreSignedUrlAsync()` using AWSSDK.S3 pre-signed URL (15-minute expiry)
- [ ] Create DTOs: `DocumentContentResponse`, `DocumentSearchResponse`, `SearchMatchDto`
- [ ] Create `IDocumentViewerService` / `DocumentViewerService`: pre-signed URL generation, full-text search with extraction status check (Edge Case 1)
- [ ] Implement `SearchExtractedTextAsync` in repository using `ts_vector`/`ts_query` with `ts_headline()` and `pg_trgm` fallback
- [ ] Add GIN index on `to_tsvector('english', extracted_text)` via EF Core migration
- [ ] Add `GET {id}/content` and `GET {id}/search` endpoints with `[Authorize(Roles = "Patient,Staff,Clinician")]` and patient-scoped access control
- [ ] Register `IDocumentViewerService` in DI container
- [ ] Sanitize search term input; enforce minimum 2-character length

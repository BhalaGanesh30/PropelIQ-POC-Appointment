---
task_id: task_002
user_story: us_043
epic: EP-006
layer: Backend
status: not-started
effort_hours: 6
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_043] Document Categorization, Rename, and Soft-Delete
- **Story Location**: [.propel/context/tasks/EP-006/us_043/us_043.md](.propel/context/tasks/EP-006/us_043/us_043.md)
- **Acceptance Criteria**:
  - AC-1: Given I have access to a patient's document list, When I assign a category to a document (e.g., Lab Report, Referral, Prescription), Then the category is saved and the document list updates to show the assigned category label.
  - AC-2: Given I view a document in the document list, When I rename it, Then the display name is updated immediately in the UI and persisted to the database, while the original storage filename is preserved.
  - AC-3: Given I want to remove a document from view, When I soft-delete a document (after confirming the confirmation dialog), Then the document is hidden from the active list but remains in the database with `IsDeleted = true`.
  - AC-4: Given I am an admin reviewing soft-deleted documents, When I access the document trash view, Then all soft-deleted documents are listed with their deletion date and I can restore them.
- **Edge Cases**:
  - Edge Case 1: Categorizing a document still processing (OCR in progress) — categorization is allowed; category is saved; OCR completion does not override the category.
  - Edge Case 2: Hard deletion prevention — no hard delete endpoint exists; `DELETE` performs soft-delete only.

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

> **Note**: US_043 references SCR-024 (Template Editor, EP-010). The document library screen is SCR-012 per figma_spec.md, which is the dedicated Document Library screen under EP-006.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
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

Implement document management API endpoints within the `ClinicalIntelligence` module for categorization, rename, soft-delete, restore, and filtered listing of clinical documents. The `DocumentsController` is extended with five new endpoints: `GET /api/v1/documents` for paginated, filtered document listing with support for category, date range, status, and soft-deleted document filters; `PATCH /api/v1/documents/{id}/category` to assign a category from a predefined enum (AC-1), allowed regardless of extraction status (Edge Case 1); `PATCH /api/v1/documents/{id}/rename` to update the display name while preserving the original storage filename (AC-2); `DELETE /api/v1/documents/{id}` for soft-delete setting `is_deleted = true` and `deleted_at` timestamp (AC-3) — no hard-delete endpoint exists (Edge Case 2); and `POST /api/v1/documents/{id}/restore` to reverse soft-deletion (AC-4, Admin role only). All endpoints enforce patient-scoped access control. The listing endpoint applies a global query filter `WHERE is_deleted = false` by default, with an `includeDeleted` parameter available to Admin users for the trash view.

---

## Dependent Tasks

- **us_040/task_002** — `DocumentsController`, `IClinicalDocumentRepository`, `ClinicalDocument` entity must exist.
- **us_043/task_003** — `display_name`, `is_deleted`, `deleted_at` columns and `document_category_type` enum must be migrated.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `DocumentsController` | MODIFY | Add list, categorize, rename, soft-delete, restore endpoints |
| `IDocumentManagementService` | CREATE | Interface: list, categorize, rename, soft-delete, restore |
| `DocumentManagementService` | CREATE | Implementation with business logic, validation, patient-scoped access |
| `DocumentListRequest` | CREATE | DTO: `PatientId`, `Category`, `DateFrom`, `DateTo`, `Status`, `IncludeDeleted`, `Page`, `PageSize` |
| `DocumentListResponse` | CREATE | DTO: `Items[]`, `TotalCount`, `Page`, `PageSize`, `TotalPages` |
| `DocumentListItemDto` | CREATE | DTO: `DocumentId`, `DisplayName`, `OriginalFilename`, `Category`, `UploadedAt`, `ExtractionStatus`, `ScanResult`, `IsDeleted`, `DeletedAt` |
| `CategorizeRequest` | CREATE | DTO: `Category` (enum value) |
| `RenameRequest` | CREATE | DTO: `DisplayName` (string, max 255) |
| `ClinicalDocument` (EF entity) | MODIFY | Add `DisplayName`, `IsDeleted`, `DeletedAt` properties |
| `IClinicalDocumentRepository` | MODIFY | Add `ListAsync(filter, page, pageSize)`, `SoftDeleteAsync`, `RestoreAsync`, `UpdateCategoryAsync`, `UpdateDisplayNameAsync` |
| `ClinicalDocumentRepository` | MODIFY | Implement filtered listing with pagination, soft-delete, restore, category/name updates |
| `ClinicalIntelligenceDbContext` | MODIFY | Add global query filter `HasQueryFilter(d => !d.IsDeleted)` on `ClinicalDocument` |
| `ClinicalIntelligenceModule` DI | MODIFY | Register `IDocumentManagementService` → `DocumentManagementService` |

---

## Implementation Plan

1. **Create DTOs** in `ClinicalIntelligence/DTOs/`:
   - `DocumentListRequest`: `Guid PatientId`, `string? Category`, `DateTime? DateFrom`, `DateTime? DateTo`, `string? Status`, `bool IncludeDeleted = false`, `int Page = 1`, `int PageSize = 20`.
   - `DocumentListItemDto`: `Guid DocumentId`, `string DisplayName`, `string OriginalFilename`, `string? Category`, `DateTime UploadedAt`, `string ExtractionStatus`, `string ScanResult`, `bool IsDeleted`, `DateTime? DeletedAt`.
   - `DocumentListResponse`: `List<DocumentListItemDto> Items`, `int TotalCount`, `int Page`, `int PageSize`, `int TotalPages`.
   - `CategorizeRequest`: `[Required] string Category` (validated against `DocumentCategory` enum values).
   - `RenameRequest`: `[Required] [MaxLength(255)] string DisplayName`.
2. **Extend `ClinicalDocument` EF entity**: Add `string DisplayName` (nullable, defaults to `OriginalFilename` when null), `bool IsDeleted` (default `false`), `DateTime? DeletedAt` (nullable).
3. **Add global query filter**: In `ClinicalIntelligenceDbContext.OnModelCreating()`, add `entity.HasQueryFilter(d => !d.IsDeleted)` to automatically exclude soft-deleted documents from all queries. For trash view queries, use `IgnoreQueryFilters()` to bypass the filter.
4. **Extend `IClinicalDocumentRepository`** with new methods:
   - `Task<(List<ClinicalDocument> Items, int TotalCount)> ListAsync(Guid patientId, string? category, DateTime? dateFrom, DateTime? dateTo, string? status, bool includeDeleted, int page, int pageSize)` — applies filters, pagination via `Skip`/`Take`, default ordering by `uploaded_at DESC`.
   - `Task UpdateCategoryAsync(Guid documentId, string category)` — update `category` column.
   - `Task UpdateDisplayNameAsync(Guid documentId, string displayName)` — update `display_name` column.
   - `Task SoftDeleteAsync(Guid documentId)` — set `is_deleted = true`, `deleted_at = DateTime.UtcNow`.
   - `Task RestoreAsync(Guid documentId)` — set `is_deleted = false`, `deleted_at = null` (requires `IgnoreQueryFilters` to find deleted document).
5. **Create `IDocumentManagementService` and `DocumentManagementService`**:
   - `ListDocumentsAsync(DocumentListRequest request, ClaimsPrincipal user)`: validate patient access, if `IncludeDeleted = true` verify Admin role, call repository `ListAsync`, map to `DocumentListResponse`.
   - `CategorizeAsync(Guid documentId, string category, ClaimsPrincipal user)`: validate document exists and user has access, validate `category` is a valid enum value, call `UpdateCategoryAsync` (AC-1). No extraction status check — categorization always allowed (Edge Case 1).
   - `RenameAsync(Guid documentId, string displayName, ClaimsPrincipal user)`: validate document exists and user has access, sanitize `displayName` (trim, max 255 chars, no path separators), call `UpdateDisplayNameAsync` (AC-2).
   - `SoftDeleteAsync(Guid documentId, ClaimsPrincipal user)`: validate document exists and user has access, call `SoftDeleteAsync` on repository (AC-3). Log deletion event for audit.
   - `RestoreAsync(Guid documentId, ClaimsPrincipal user)`: verify Admin role, find deleted document via `IgnoreQueryFilters`, call `RestoreAsync` on repository (AC-4). Log restoration event.
6. **Extend `DocumentsController`** with new endpoints:
   - `[HttpGet] [Authorize(Roles = "Clinician,Staff,Admin")]`: accept `[FromQuery] DocumentListRequest`, return `200 OK` with `DocumentListResponse`. `IncludeDeleted` param only honoured for Admin role.
   - `[HttpPatch("{id}/category")] [Authorize(Roles = "Clinician,Staff")]`: accept `[FromBody] CategorizeRequest`, return `204 NoContent`.
   - `[HttpPatch("{id}/rename")] [Authorize(Roles = "Clinician,Staff")]`: accept `[FromBody] RenameRequest`, return `204 NoContent`.
   - `[HttpDelete("{id}")] [Authorize(Roles = "Clinician,Staff")]`: soft-delete, return `204 NoContent`. No hard-delete endpoint exists (Edge Case 2).
   - `[HttpPost("{id}/restore")] [Authorize(Roles = "Admin")]`: restore from soft-delete, return `204 NoContent`.
7. **Register services**: Register `IDocumentManagementService` → `DocumentManagementService` (Scoped) in `ClinicalIntelligenceModule` DI.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   └── DocumentsController.cs                ← MODIFY (add 5 new endpoints)
│   │   ├── DTOs/
│   │   │   ├── DocumentListRequest.cs                ← CREATE
│   │   │   ├── DocumentListResponse.cs               ← CREATE
│   │   │   ├── DocumentListItemDto.cs                ← CREATE
│   │   │   ├── CategorizeRequest.cs                  ← CREATE
│   │   │   ├── RenameRequest.cs                      ← CREATE
│   │   │   └── [existing DTOs from US_040-US_042...]
│   │   ├── Entities/
│   │   │   └── ClinicalDocument.cs                   ← MODIFY (add DisplayName, IsDeleted, DeletedAt)
│   │   ├── Repositories/
│   │   │   ├── IClinicalDocumentRepository.cs        ← MODIFY (add list, soft-delete, restore, update methods)
│   │   │   └── ClinicalDocumentRepository.cs         ← MODIFY (implement new methods with IgnoreQueryFilters)
│   │   ├── Services/
│   │   │   ├── IDocumentManagementService.cs         ← CREATE
│   │   │   ├── DocumentManagementService.cs          ← CREATE
│   │   │   └── [existing services...]
│   │   └── Data/
│   │       └── ClinicalIntelligenceDbContext.cs      ← MODIFY (add global query filter)
│   └── [existing modules...]
└── [existing project structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentListRequest.cs` | Paginated filter DTO: PatientId, Category, DateFrom, DateTo, Status, IncludeDeleted, Page, PageSize |
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentListResponse.cs` | Paginated response DTO: Items, TotalCount, Page, PageSize, TotalPages |
| CREATE | `Modules/ClinicalIntelligence/DTOs/DocumentListItemDto.cs` | List item DTO: DocumentId, DisplayName, OriginalFilename, Category, UploadedAt, ExtractionStatus, ScanResult, IsDeleted, DeletedAt |
| CREATE | `Modules/ClinicalIntelligence/DTOs/CategorizeRequest.cs` | Request DTO: Category (validated enum value) |
| CREATE | `Modules/ClinicalIntelligence/DTOs/RenameRequest.cs` | Request DTO: DisplayName (max 255 chars) |
| CREATE | `Modules/ClinicalIntelligence/Services/IDocumentManagementService.cs` | Interface: list, categorize, rename, soft-delete, restore |
| CREATE | `Modules/ClinicalIntelligence/Services/DocumentManagementService.cs` | Business logic with patient-scoped access, role validation, audit logging |
| MODIFY | `Modules/ClinicalIntelligence/Controllers/DocumentsController.cs` | Add GET list, PATCH category, PATCH rename, DELETE soft-delete, POST restore |
| MODIFY | `Modules/ClinicalIntelligence/Entities/ClinicalDocument.cs` | Add DisplayName, IsDeleted, DeletedAt properties |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalDocumentRepository.cs` | Add ListAsync, UpdateCategoryAsync, UpdateDisplayNameAsync, SoftDeleteAsync, RestoreAsync |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalDocumentRepository.cs` | Implement filtered listing, soft-delete/restore with IgnoreQueryFilters |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Add global query filter `HasQueryFilter(d => !d.IsDeleted)` |

---

## External References

- EF Core Global Query Filters: https://learn.microsoft.com/en-us/ef/core/querying/filters
- EF Core IgnoreQueryFilters: https://learn.microsoft.com/en-us/ef/core/querying/filters#disabling-filters
- ASP.NET Core Model Validation: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation
- FR-DM-004: System MUST support document categorization, rename, and soft-delete operations
- UXR-111: All destructive actions MUST require confirmation dialog (frontend concern, but API must support undo/restore)

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

- [ ] Unit tests pass for `DocumentManagementService` — list with filters, categorize, rename, soft-delete, restore
- [ ] Unit tests pass for categorization of documents with any `extraction_status` (Edge Case 1)
- [ ] Unit tests pass for soft-delete setting `IsDeleted = true` and `DeletedAt` timestamp
- [ ] Unit tests pass for restore clearing `IsDeleted` and `DeletedAt`
- [ ] Integration tests pass for `GET /api/v1/documents` — filtered listing, pagination, soft-deleted excluded by default
- [ ] Integration tests pass for `GET /api/v1/documents?includeDeleted=true` — Admin-only, returns soft-deleted documents (AC-4)
- [ ] Integration tests pass for `PATCH {id}/category` — 204 on success, 400 for invalid category (AC-1)
- [ ] Integration tests pass for `PATCH {id}/rename` — 204 on success, 400 for empty/too-long name (AC-2)
- [ ] Integration tests pass for `DELETE {id}` — 204 soft-delete, document hidden from active list (AC-3)
- [ ] Integration tests pass for `POST {id}/restore` — 204 Admin only, 403 for non-Admin (AC-4)
- [ ] No hard-delete endpoint exists — verified by API route inspection (Edge Case 2)
- [ ] Patient-scoped access control enforced — users cannot manage documents of unauthorized patients

---

## Implementation Checklist

- [ ] Create DTOs: `DocumentListRequest`, `DocumentListResponse`, `DocumentListItemDto`, `CategorizeRequest`, `RenameRequest`
- [ ] Extend `ClinicalDocument` entity with `DisplayName`, `IsDeleted`, `DeletedAt`; add global query filter
- [ ] Extend `IClinicalDocumentRepository` with `ListAsync`, `UpdateCategoryAsync`, `UpdateDisplayNameAsync`, `SoftDeleteAsync`, `RestoreAsync` (using `IgnoreQueryFilters` for restore)
- [ ] Create `IDocumentManagementService` / `DocumentManagementService`: patient-scoped validation, role checks, audit logging
- [ ] Add `GET` list endpoint with pagination and filters; `IncludeDeleted` honoured only for Admin role (AC-4)
- [ ] Add `PATCH {id}/category` (AC-1, Edge Case 1) and `PATCH {id}/rename` (AC-2) endpoints
- [ ] Add `DELETE {id}` for soft-delete (AC-3) and `POST {id}/restore` for Admin restore (AC-4); no hard-delete (Edge Case 2)
- [ ] Register `IDocumentManagementService` in DI container

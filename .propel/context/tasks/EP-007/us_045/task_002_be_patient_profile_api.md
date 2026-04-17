---
task_id: task_002
user_story: us_045
epic: EP-007
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_045] 360-Degree Patient Profile View
- **Story Location**: [.propel/context/tasks/EP-007/us_045/us_045.md](.propel/context/tasks/EP-007/us_045/us_045.md)
- **Acceptance Criteria**:
  - AC-1: Given I open a patient profile, When the page loads, Then a consolidated view of medications, allergies, diagnoses, and timeline entries is rendered within 3 seconds p95 — requires API response ≤500ms p95 (NFR-002) and Redis caching (TR-004).
  - AC-2: Given the profile is displayed, When I click on a data point, Then a source traceability link to the originating document is shown — requires each fact in the response to carry `document_id`, `document_display_name`, and `uploaded_at` references.
  - AC-3: Given I view a medication entry, When I request its metadata, Then document name, upload date, and extraction confidence are visible — requires `source_document` projection in the response DTO.
  - AC-4: Given a profile has no clinical data, When the API is called, Then an empty facts collection is returned (not a 404), enabling the FE to render the empty state.
- **Edge Cases**:
  - Edge Case 1: One data query fails (e.g., diagnoses query errors) — API returns partial data with `partial_sources` array listing unavailable categories; HTTP 206 Partial Content or HTTP 200 with `partial: true` flag.
  - Edge Case 2: Large profiles (100+ facts) — API supports `?tab=summary&limit=50&offset=0` pagination parameters for each fact category; total count returned to enable FE virtual scrolling.

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

Implement the `GET /api/v1/patients/{id}/profile` endpoint in the `ClinicalIntelligence` module as a profile aggregation API. The endpoint queries `clinical_facts` grouped by `fact_type` (medication, allergy, diagnosis, finding) with a JOIN on `clinical_documents` to provide per-fact source traceability (AC-2, AC-3). Each fact in the response DTO includes: `factId`, `factType`, `name`, `value`, `confidenceScore`, `needsReview`, `verified`, and a nested `sourceDocument` object (`documentId`, `displayName`, `uploadedAt`). Timeline entries are derived from facts ordered by `fact_date`. The aggregation response is cached in Redis with a 60-second TTL to satisfy the 500ms p95 API target (NFR-002, TR-004). Pagination via `?limit` and `?offset` query parameters supports large profiles (Edge Case 2). Partial failure resilience: each fact category is queried independently; if one query fails, available data is returned with a `partialSources` collection identifying the failed category (Edge Case 1). Empty profiles return HTTP 200 with empty `facts` collections (not 404) to enable the FE empty state (AC-4). Role-based authorization enforces `Clinician` or `Staff` roles (read-only per SCR-014 access matrix). OpenTelemetry spans track total aggregation time to surface latency regressions.

---

## Dependent Tasks

- **us_044/task_002** — `clinical_facts` table must be migrated (required for data queries).
- **us_040/task_003** — `clinical_documents` table must be migrated (required for source JOIN).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `PatientProfileController` | CREATE | `GET /api/v1/patients/{id}/profile` with `[Authorize(Roles = "Clinician,Staff")]` |
| `IPatientProfileAggregationService` | CREATE | Interface: `AggregateProfileAsync(Guid patientId, ProfileQuery query, CancellationToken ct)` |
| `PatientProfileAggregationService` | CREATE | Parallel per-category queries with partial failure handling |
| `ProfileQuery` | CREATE | Record: `int Limit`, `int Offset`, `string Tab` |
| `PatientProfileDto` | CREATE | Response: `PatientSummaryDto`, `List<ClinicalFactDto>`, `List<PartialSourceDto>`, `PaginationDto` |
| `ClinicalFactDto` | CREATE | Projection: factId, factType, name, value, confidenceScore, needsReview, verified, sourceDocument |
| `SourceDocumentDto` | CREATE | Nested: documentId, displayName, uploadedAt |
| `PartialSourceDto` | CREATE | Record: `string Category`, `string ErrorReason` — returned in partial failure (Edge Case 1) |
| `IProfileCacheService` | CREATE | Interface: cache profile by `patientId` with 60-second TTL |
| `ProfileCacheService` | CREATE | Redis implementation using StackExchange.Redis with JSON serialization |
| `IClinicalFactRepository` | MODIFY | Add: `GetByPatientIdGroupedAsync(Guid patientId, string factType, int limit, int offset)` |
| `ClinicalFactRepository` | MODIFY | Implement new query method with EF Core projection + JOIN on `clinical_documents` |

---

## Implementation Plan

1. **Create `ProfileQuery` record**: Properties `int Limit` (default `50`, max `100`), `int Offset` (default `0`), `string Tab` (default `"summary"`). Validate in controller: `Limit` must be 1–100.
2. **Create response DTOs**:
   - `SourceDocumentDto`: `Guid DocumentId`, `string DisplayName`, `DateTimeOffset UploadedAt`.
   - `ClinicalFactDto`: `Guid FactId`, `string FactType`, `string Name`, `string Value`, `decimal ConfidenceScore`, `bool NeedsReview`, `bool Verified`, `SourceDocumentDto SourceDocument`.
   - `PartialSourceDto`: `string Category`, `string ErrorReason`.
   - `PatientProfileDto`: `Guid PatientId`, `List<ClinicalFactDto> Medications`, `List<ClinicalFactDto> Allergies`, `List<ClinicalFactDto> Diagnoses`, `List<ClinicalFactDto> Findings`, `List<ClinicalFactDto> Timeline`, `bool Partial`, `List<PartialSourceDto> PartialSources`, `int TotalCount`.
3. **Extend `IClinicalFactRepository`**: Add `Task<(List<ClinicalFact> Facts, int Total)> GetByPatientIdGroupedAsync(Guid patientId, string factType, int limit, int offset, CancellationToken ct)`. Implement with EF Core LINQ: `.Where(f => f.PatientId == patientId && f.FactType == factType).Include(f => f.ClinicalDocument).OrderByDescending(f => f.FactDate).Skip(offset).Take(limit)`, returning results and total count via `.CountAsync()`.
4. **Create `IPatientProfileAggregationService` and `PatientProfileAggregationService`**: For each fact category (medication, allergy, diagnosis, finding), wrap the repository call in a `try/catch`. On success, add results to the corresponding DTO list. On failure, add to `partialSources` with the category name and exception message (sanitized — no internal stack traces). Run categories in parallel using `Task.WhenAll` for minimum latency. Build `Timeline` list by aggregating all facts and ordering by `fact_date` descending. Set `Partial = true` when any category fails.
5. **Create `IProfileCacheService` and `ProfileCacheService`**: Cache key: `profile:{patientId}:limit:{limit}:offset:{offset}`. Serialize `PatientProfileDto` as JSON. Cache TTL: 60 seconds. On cache miss, call aggregation service and populate. On cache hit, deserialize and return. Invalidation: not required for this story (extraction pipeline writes new facts independently).
6. **Create `PatientProfileController`**: `[Route("api/v1/patients")]` with `[ApiController]`. `[HttpGet("{id}/profile")]` action. Validate `id` is a valid `Guid`. Validate `query.Limit` (1–100). Return `HTTP 200 OK` with `PatientProfileDto` — always 200, even when empty (AC-4). Do not return 404 for empty profiles. If `partial = true`, set `X-Partial-Content: true` response header for FE to display warning (Edge Case 1). Apply `[Authorize(Roles = "Clinician,Staff")]`.
7. **Add OpenTelemetry instrumentation**: Wrap aggregation in an `Activity` named `PatientProfileAggregationService.AggregateAsync`. Add tags: `patient.id`, `query.tab`, `result.total_facts`, `result.partial`. Emit metric `profile.aggregation.duration_ms` (Edge Case 2 — helps detect when large profiles degrade performance).
8. **Register services in DI**: Add `IPatientProfileAggregationService`, `ProfileCacheService` (singleton), and updated repository in `ClinicalIntelligenceModule` DI registration.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── DocumentsController.cs            ← EXISTS (US_040, US_042, US_043)
│   │   │   └── PatientProfileController.cs       ← CREATE
│   │   ├── Services/
│   │   │   ├── IClinicalExtractionService.cs     ← EXISTS (US_044)
│   │   │   ├── IPatientProfileAggregationService.cs  ← CREATE
│   │   │   └── PatientProfileAggregationService.cs   ← CREATE
│   │   ├── Cache/
│   │   │   ├── IProfileCacheService.cs           ← CREATE
│   │   │   └── ProfileCacheService.cs            ← CREATE
│   │   ├── DTOs/
│   │   │   ├── ClinicalFactDto.cs                ← CREATE
│   │   │   ├── SourceDocumentDto.cs              ← CREATE
│   │   │   ├── PartialSourceDto.cs               ← CREATE
│   │   │   ├── PatientProfileDto.cs              ← CREATE
│   │   │   └── ProfileQuery.cs                   ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalFactRepository.cs        ← MODIFY (add grouped query method)
│   │   │   └── ClinicalFactRepository.cs         ← MODIFY (implement grouped query)
│   │   └── [existing module structure...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/PatientProfileController.cs` | GET /api/v1/patients/{id}/profile with Clinician/Staff authorization |
| CREATE | `Modules/ClinicalIntelligence/Services/IPatientProfileAggregationService.cs` | Aggregation interface |
| CREATE | `Modules/ClinicalIntelligence/Services/PatientProfileAggregationService.cs` | Parallel per-category queries with partial failure handling |
| CREATE | `Modules/ClinicalIntelligence/Cache/IProfileCacheService.cs` | Cache interface |
| CREATE | `Modules/ClinicalIntelligence/Cache/ProfileCacheService.cs` | Redis cache with 60s TTL |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ClinicalFactDto.cs` | Fact projection with source document nested object |
| CREATE | `Modules/ClinicalIntelligence/DTOs/SourceDocumentDto.cs` | Nested: documentId, displayName, uploadedAt |
| CREATE | `Modules/ClinicalIntelligence/DTOs/PartialSourceDto.cs` | Partial failure: category name + sanitized error |
| CREATE | `Modules/ClinicalIntelligence/DTOs/PatientProfileDto.cs` | Full profile response: facts by category, partial flag, pagination |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ProfileQuery.cs` | Query record: limit, offset, tab |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalFactRepository.cs` | Add GetByPatientIdGroupedAsync with pagination |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalFactRepository.cs` | EF Core projection with JOIN on clinical_documents |

---

## External References

- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- EF Core Include/projection: https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/dotnet/
- Task.WhenAll parallel queries: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall
- FR-CA-002: Unified 360-degree patient profile in under 3 seconds with source traceability links
- NFR-002: API responses within 500ms p95 for profile retrieval endpoints
- NFR-004: 500 concurrent active users
- TR-004: Redis caching for profile read acceleration with bounded TTL
- SCR-014 access matrix: Clinician (Read/Write), Staff (Read)

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

- [ ] `GET /api/v1/patients/{id}/profile` returns HTTP 200 with full `PatientProfileDto` for a patient with existing facts
- [ ] Response DTO includes `medications`, `allergies`, `diagnoses`, `findings`, and `timeline` collections
- [ ] Each fact in the response contains nested `sourceDocument` with `displayName`, `uploadedAt`, `documentId` (AC-2, AC-3)
- [ ] Empty patient profile returns HTTP 200 with empty fact collections (not 404) — enables FE empty state (AC-4)
- [ ] When one category query fails, response returns available categories with `partial: true` and `partialSources` listing failed category (Edge Case 1)
- [ ] Redis caches profile response on first call; second identical call is served from cache (TR-004)
- [ ] Cache TTL is 60 seconds — verified by inspecting Redis TTL key
- [ ] Pagination: `?limit=10&offset=0` returns first 10 facts per category with `totalCount` for each
- [ ] `Limit` > 100 returns HTTP 400 Bad Request
- [ ] Unauthorized caller receives HTTP 401; caller with wrong role receives HTTP 403
- [ ] API response time < 500ms p95 under load simulation (NFR-002)
- [ ] OpenTelemetry span `PatientProfileAggregationService.AggregateAsync` recorded with patient.id and result.total_facts tags

---

## Implementation Checklist

- [ ] Create `ProfileQuery`, `ClinicalFactDto`, `SourceDocumentDto`, `PartialSourceDto`, `PatientProfileDto` DTOs
- [ ] Extend `IClinicalFactRepository` and `ClinicalFactRepository` with `GetByPatientIdGroupedAsync` (pagination + JOIN on clinical_documents)
- [ ] Create `IPatientProfileAggregationService` / `PatientProfileAggregationService` with parallel category queries and partial failure handling (Edge Case 1)
- [ ] Create `IProfileCacheService` / `ProfileCacheService` using Redis with 60-second TTL (TR-004, NFR-002)
- [ ] Create `PatientProfileController` with `GET /api/v1/patients/{id}/profile`; return HTTP 200 always (even empty); add `X-Partial-Content` header on partial (Edge Case 1)
- [ ] Apply `[Authorize(Roles = "Clinician,Staff")]` and validate `Limit` 1–100 with HTTP 400 on violation
- [ ] Add OpenTelemetry instrumentation with aggregation span and `profile.aggregation.duration_ms` metric
- [ ] Register all new services in `ClinicalIntelligenceModule` DI registration

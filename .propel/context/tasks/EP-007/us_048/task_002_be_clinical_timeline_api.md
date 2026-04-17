---
task_id: task_002
user_story: us_048
epic: EP-007
layer: Backend
status: not-started
effort_hours: 5
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_048] Chronological Clinical Timeline View
- **Story Location**: [.propel/context/tasks/EP-007/us_048/us_048.md](.propel/context/tasks/EP-007/us_048/us_048.md)
- **Acceptance Criteria**:
  - AC-1: Given the timeline endpoint is called, Then events are returned in reverse chronological order covering medications, diagnoses, allergies, and documents.
  - AC-2: Given a category filter is applied, Then only matching event types are returned within 500 ms (NFR-002).
  - AC-3: Given a date range filter is applied, Then only events within the specified range are returned.
  - AC-4: Given "Print Timeline" is clicked, Then FE calls the same endpoint with active filters; server returns the filtered list formatted for print rendering (no separate print endpoint required).
- **Edge Cases**:
  - Edge Case 1: No events for patient — endpoint returns HTTP 200 with empty `events` array (not 404).
  - Edge Case 2: Very long timelines — server-side filtering via `category` and `dateFrom`/`dateTo` query parameters; response includes `totalCount` to inform FE grouping and virtual scroll.

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

Implement `GET /api/v1/patients/{id}/timeline` in the `ClinicalIntelligence` module. The endpoint aggregates timeline events from two sources:

1. **`clinical_facts`** — each fact produces an event: `fact_type` maps to category (medication → "Medications", allergy → "Allergies", diagnosis → "Diagnoses", finding → "Findings"), with `fact_date` as the event date and `name`+`value` composing the description.
2. **`clinical_documents`** — each uploaded document produces a "Documents" category event with `display_name` as description and `uploaded_at` as event date.

Both sources are merged, sorted in reverse chronological order by event date (`DESC`), and returned as a unified `TimelineEventDto` list. Server-side filtering via optional query parameters: `category` (case-insensitive match against event category), `dateFrom` and `dateTo` (ISO 8601 date strings, inclusive range) applied at query time to support performance on large timelines (Edge Case 2, NFR-002). Empty result set returns HTTP 200 with `{ events: [], totalCount: 0 }` (Edge Case 1). Response cached in Redis with a 60-second TTL keyed by `patientId + filters`. Role-based authorization: `Clinician` and `Staff` (read-only per SCR-015 access matrix). OpenTelemetry span tracks total query time.

---

## Dependent Tasks

- **us_044/task_002** — `clinical_facts` table must exist.
- **us_040/task_003** — `clinical_documents` table with `display_name` and `uploaded_at` must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `TimelineController` | CREATE | `GET /api/v1/patients/{id}/timeline` with `[Authorize(Roles = "Clinician,Staff")]` |
| `ITimelineService` | CREATE | Interface: `GetTimelineAsync(Guid patientId, TimelineQuery query, CancellationToken ct)` |
| `TimelineService` | CREATE | Aggregates facts + documents, merges, sorts, applies filters, returns paged result |
| `TimelineQuery` | CREATE | Record: `string? Category`, `DateTimeOffset? DateFrom`, `DateTimeOffset? DateTo` |
| `TimelineEventDto` | CREATE | `Guid EventId`, `string EventType`, `string Category`, `string Description`, `DateTimeOffset EventDate` |
| `TimelineResponseDto` | CREATE | `List<TimelineEventDto> Events`, `int TotalCount` |
| `ITimelineCacheService` | CREATE | Interface: cache timeline responses per patientId + filter hash |
| `TimelineCacheService` | CREATE | Redis with 60-second TTL; cache key includes `patientId`, category, dateFrom, dateTo |
| `IClinicalFactRepository` | MODIFY | Add: `GetTimelineFactsAsync(Guid patientId, string? factType, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)` |
| `ClinicalFactRepository` | MODIFY | Implement timeline query: project to `TimelineEventDto`; apply type + date filters |
| `IDocumentRepository` | MODIFY | Add: `GetTimelineDocumentsAsync(Guid patientId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)` |
| `DocumentRepository` | MODIFY | Implement timeline projection from `clinical_documents` with date filter |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new services |

---

## Implementation Plan

1. **Create `TimelineQuery` record**: `string? Category` (nullable; when set, applied as case-insensitive filter on event source mapping), `DateTimeOffset? DateFrom`, `DateTimeOffset? DateTo`. Validate in controller: if `DateFrom` and `DateTo` are both set, `DateFrom` must be <= `DateTo` (return HTTP 400 otherwise).
2. **Create `TimelineEventDto` and `TimelineResponseDto`**: `TimelineEventDto`: `Guid EventId` (source row's PK), `string EventType` (e.g., "fact_added", "document_uploaded"), `string Category` ("Medications"/"Allergies"/"Diagnoses"/"Findings"/"Documents"), `string Description` (composed from name/value or document display_name), `DateTimeOffset EventDate`. `TimelineResponseDto`: `List<TimelineEventDto> Events` (reverse-chronological), `int TotalCount`.
3. **Extend `IClinicalFactRepository`**: Add `GetTimelineFactsAsync(Guid patientId, string? factType, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)`. EF Core LINQ: `.Where(f => f.PatientId == patientId)`, apply `f.FactType == factType` when set, apply `f.FactDate >= from` and `f.FactDate <= to` when set. Project to `TimelineEventDto`: `EventId = f.FactId`, `EventType = "fact_added"`, `Category = MapFactTypeToCategory(f.FactType)`, `Description = $"{f.Name}: {f.Value}"`, `EventDate = f.FactDate ?? f.CreatedAt`.
4. **Extend `IDocumentRepository`**: Add `GetTimelineDocumentsAsync(Guid patientId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)`. EF Core: `.Where(d => d.PatientId == patientId && !d.IsDeleted)`, apply date filters on `d.UploadedAt`. Project to `TimelineEventDto`: `EventId = d.DocumentId`, `EventType = "document_uploaded"`, `Category = "Documents"`, `Description = d.DisplayName`, `EventDate = d.UploadedAt`.
5. **Create `ITimelineService` and `TimelineService`**: 
   - Determine which sources to query based on `query.Category`:
     - `null` or `"All"` → query both facts and documents.
     - `"Documents"` → query documents only.
     - Any other category → query facts only with corresponding `factType` filter.
   - Execute queries in parallel using `Task.WhenAll` when both sources needed.
   - Merge results into a single list; sort `OrderByDescending(e => e.EventDate)`.
   - Return `new TimelineResponseDto { Events = merged, TotalCount = merged.Count }`.
6. **Create `ITimelineCacheService` and `TimelineCacheService`**: Cache key: `timeline:{patientId}:cat:{category ?? "all"}:from:{dateFrom ?? "none"}:to:{dateTo ?? "none"}`. Serialize `TimelineResponseDto` as JSON with 60-second TTL. On cache miss, call service and populate.
7. **Create `TimelineController`**: `[HttpGet("patients/{id}/timeline")]`. Bind query parameters to `TimelineQuery` via `[FromQuery]`. Validate `id` as Guid; validate date range order. Check cache; on miss call `TimelineService`, cache result, return. Apply `[Authorize(Roles = "Clinician,Staff")]`. Return `HTTP 200 OK` with `TimelineResponseDto` — always 200 even for empty results (Edge Case 1).
8. **Add OpenTelemetry instrumentation**: Wrap `TimelineService.GetTimelineAsync` in an Activity. Tags: `patient.id`, `query.category`, `result.total_events`. Metric: `timeline.query.duration_ms`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── DocumentsController.cs            ← EXISTS (US_040-US_043)
│   │   │   ├── PatientProfileController.cs       ← EXISTS (US_045)
│   │   │   ├── ConflictController.cs             ← EXISTS (US_046)
│   │   │   ├── ClinicalFactsController.cs        ← EXISTS (US_047)
│   │   │   └── TimelineController.cs             ← CREATE
│   │   ├── Services/
│   │   │   ├── IPatientProfileAggregationService.cs  ← EXISTS (US_045)
│   │   │   ├── IConflictDetectionService.cs      ← EXISTS (US_046)
│   │   │   ├── IFactEditingService.cs            ← EXISTS (US_047)
│   │   │   ├── ITimelineService.cs               ← CREATE
│   │   │   └── TimelineService.cs                ← CREATE
│   │   ├── Cache/
│   │   │   ├── ProfileCacheService.cs            ← EXISTS (US_045)
│   │   │   ├── ConflictCacheService.cs           ← EXISTS (US_046)
│   │   │   ├── ITimelineCacheService.cs          ← CREATE
│   │   │   └── TimelineCacheService.cs           ← CREATE
│   │   ├── DTOs/
│   │   │   ├── PatientProfileDto.cs              ← EXISTS (US_045)
│   │   │   ├── TimelineEventDto.cs               ← CREATE
│   │   │   ├── TimelineResponseDto.cs            ← CREATE
│   │   │   └── TimelineQuery.cs                  ← CREATE
│   │   ├── Repositories/
│   │   │   ├── IClinicalFactRepository.cs        ← MODIFY (add GetTimelineFactsAsync)
│   │   │   ├── ClinicalFactRepository.cs         ← MODIFY (implement timeline projection)
│   │   │   ├── IDocumentRepository.cs            ← MODIFY (add GetTimelineDocumentsAsync)
│   │   │   └── DocumentRepository.cs             ← MODIFY (implement timeline projection)
│   │   └── [existing module structure...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/TimelineController.cs` | GET /api/v1/patients/{id}/timeline (Clinician+Staff); validates date range order; always HTTP 200 |
| CREATE | `Modules/ClinicalIntelligence/Services/ITimelineService.cs` | Timeline aggregation interface |
| CREATE | `Modules/ClinicalIntelligence/Services/TimelineService.cs` | Parallel source queries, merge, sort, category/date filter |
| CREATE | `Modules/ClinicalIntelligence/Cache/ITimelineCacheService.cs` | Cache interface |
| CREATE | `Modules/ClinicalIntelligence/Cache/TimelineCacheService.cs` | Redis 60s TTL keyed by patientId + filter hash |
| CREATE | `Modules/ClinicalIntelligence/DTOs/TimelineEventDto.cs` | Event: eventId, eventType, category, description, eventDate |
| CREATE | `Modules/ClinicalIntelligence/DTOs/TimelineResponseDto.cs` | Response: events list (reverse-chron) + totalCount |
| CREATE | `Modules/ClinicalIntelligence/DTOs/TimelineQuery.cs` | Query record: Category, DateFrom, DateTo |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IClinicalFactRepository.cs` | Add GetTimelineFactsAsync with factType + date filters |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/ClinicalFactRepository.cs` | Project facts to TimelineEventDto with category mapping |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/IDocumentRepository.cs` | Add GetTimelineDocumentsAsync with date filter |
| MODIFY | `Modules/ClinicalIntelligence/Repositories/DocumentRepository.cs` | Project documents to TimelineEventDto |

---

## External References

- EF Core LINQ projection: https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager
- Task.WhenAll parallel queries: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall
- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- OpenTelemetry .NET: https://opentelemetry.io/docs/languages/dotnet/
- FR-CA-005: Chronological timeline view with filter and print support
- NFR-002: API responses within 500 ms p95 — server-side filtering required for large timelines
- TR-004: Redis caching for profile read acceleration with bounded TTL
- SCR-015 access matrix: Clinician (Read), Staff (Read)

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

- [ ] `GET /api/v1/patients/{id}/timeline` returns HTTP 200 with events ordered by `eventDate DESC` (AC-1)
- [ ] Response includes events from both `clinical_facts` (medication, allergy, diagnosis, finding) and `clinical_documents` sources
- [ ] `?category=Medications` returns only medication facts; `?category=Documents` returns only documents (AC-2)
- [ ] `?dateFrom=2025-01-01&dateTo=2025-12-31` returns only events within range (AC-3)
- [ ] Invalid date range (`dateFrom > dateTo`) returns HTTP 400
- [ ] Empty patient timeline returns HTTP 200 with `{ events: [], totalCount: 0 }` (Edge Case 1)
- [ ] Redis cache served on second identical request; `totalCount` matches actual event count (Edge Case 2)
- [ ] Unauthorized caller returns HTTP 401; Patient role returns HTTP 403
- [ ] OpenTelemetry span and `timeline.query.duration_ms` metric emitted per request

---

## Implementation Checklist

- [ ] Create `TimelineQuery`, `TimelineEventDto`, `TimelineResponseDto` DTOs
- [ ] Extend `IClinicalFactRepository` / `ClinicalFactRepository` with `GetTimelineFactsAsync` projecting facts to `TimelineEventDto` with category mapping and date filter
- [ ] Extend `IDocumentRepository` / `DocumentRepository` with `GetTimelineDocumentsAsync` projecting documents to `TimelineEventDto`
- [ ] Create `ITimelineService` / `TimelineService`: parallel source queries, category-based routing, merge + sort, return response (Edge Case 2)
- [ ] Create `ITimelineCacheService` / `TimelineCacheService` with 60s Redis TTL keyed by patientId + filter hash (TR-004)
- [ ] Create `TimelineController`: GET with category + date params, date range validation, always HTTP 200 (Edge Case 1), Clinician+Staff authorization
- [ ] Add OpenTelemetry span + `timeline.query.duration_ms` metric; register services in DI

---
task_id: task_002
user_story: us_052
epic: EP-008
layer: Backend
status: completed
effort_hours: 6
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_052] Code Search with Autocomplete and Favorites
- **Story Location**: [.propel/context/tasks/EP-008/us_052/us_052.md](.propel/context/tasks/EP-008/us_052/us_052.md)
- **Acceptance Criteria**:
  - AC-1: Given I type at least 2 characters in the code search field, Then matching codes are returned within 500 ms (NFR-002).
  - AC-2: Given search results are displayed, When I click on a code, Then the code is added to the current encounter's coding record — manual code selection persisted as a `coding_decisions` row via existing `CodingDecisionRepository`.
  - AC-3: Given I click the "Favorite" star, Then the code is added to my personal favorites and appears at the top of future results.
  - AC-4: Given I click "Unfavorite", Then the code is removed from my favorites list and the change is persisted immediately.
- **Edge Cases**:
  - Edge Case 1: No results — return HTTP 200 with `{ results: [], totalCount: 0 }`.
  - Edge Case 2: Deprecated codes — excluded from results by default (`is_deprecated = false` filter applied); when `?includeDeprecated=true` query param is passed, deprecated codes are included.

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
| Database | PostgreSQL 15.x (pg_trgm extension) | 15.x |
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

Implement code search and favorites management endpoints in the `ClinicalIntelligence` module. FR-MC-004 is `[DETERMINISTIC]` — no AI involvement; pure database search.

**Search endpoint** `GET /api/v1/codes/search?q=&type=all|icd10|cpt&includeDeprecated=false&limit=20`: Uses PostgreSQL `pg_trgm` trigram similarity search on both `icd_codes` (task_003) and `cpt_codes` (US_050/task_003). Query plan: UNION of trigram similarity searches on `(code ILIKE :q OR description ILIKE :q)` filtered by `is_deprecated` status (Edge Case 2). Results merged and sorted by trigram similarity score descending. User's favorites are joined in and pinned first. Redis cache with 60-second TTL keyed by `(q, type, includeDeprecated)` — query-level cache shared across users (favorites position adjusted per-user at merge time).

**Favorites endpoints**: `GET /api/v1/users/me/code-favorites` — reads user's favorites from `user_code_favorites` (task_003) and joins `icd_codes`/`cpt_codes` for code descriptions. `POST /api/v1/users/me/code-favorites` body `{ code, codeType }` — inserts row; validates code exists in the reference table (HTTP 422 if not found). `DELETE /api/v1/users/me/code-favorites/{codeType}/{code}` — removes row; HTTP 204 on success, HTTP 404 if not in favorites.

**Manual code selection** (AC-2): A thin `POST /api/v1/coding-decisions/manual` endpoint accepts `{ patientId, code, codeType, description }` and delegates to the existing `CodingDecisionRepository.InsertPendingAsync` with `reviewer_action = accepted` (manual selection is an immediate finalized decision, not pending AI review).

---

## Dependent Tasks

- **us_052/task_003** — `icd_codes` and `user_code_favorites` tables must exist.
- **us_050/task_003** — `cpt_codes` table must exist.
- **us_049/task_003** — `coding_decisions` table must exist for manual code selection (AC-2).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `CodeSearchController` | CREATE | GET /api/v1/codes/search; GET/POST/DELETE /api/v1/users/me/code-favorites; POST /api/v1/coding-decisions/manual |
| `ICodeSearchService` | CREATE | Interface: `SearchAsync`, `GetFavoritesAsync`, `AddFavoriteAsync`, `RemoveFavoriteAsync` |
| `CodeSearchService` | CREATE | Trigram UNION search + favorites merge; per-query Redis cache; deprecated filter |
| `ICodeFavoriteRepository` | CREATE | Interface: CRUD on `user_code_favorites`; validate code exists in reference table |
| `CodeFavoriteRepository` | CREATE | EF Core queries on `user_code_favorites`; join icd_codes/cpt_codes for description |
| `ICodeReferenceRepository` | CREATE | Interface: `SearchAsync(q, type, includeDeprecated, limit)` on icd_codes + cpt_codes UNION |
| `CodeReferenceRepository` | CREATE | Raw SQL UNION with `pg_trgm` `similarity()` scoring; apply deprecated filter; ORDER BY similarity DESC |
| `CodeResultDto` | CREATE | `{ Code, Description, CodeType, IsDeprecated, IsFavorited }` |
| `CodeSearchResponseDto` | CREATE | `{ Results: List<CodeResultDto>, TotalCount: int }` |
| `AddFavoriteRequestDto` | CREATE | `{ Code: string, CodeType: 'icd10'|'cpt' }` |
| `ManualCodeSelectionRequestDto` | CREATE | `{ PatientId: Guid, Code: string, CodeType: string, Description: string }` |
| `CodingDecisionRepository` | REUSE | US_049 `InsertPendingAsync` — call with `reviewer_action = accepted` for manual selection |
| `ClinicalIntelligenceModule` DI | MODIFY | Register new services and repositories |

---

## Implementation Plan

1. **Create DTOs**: `CodeResultDto` (`string Code`, `string Description`, `string CodeType`, `bool IsDeprecated`, `bool IsFavorited`); `CodeSearchResponseDto` (`List<CodeResultDto> Results`, `int TotalCount`); `AddFavoriteRequestDto` (`string Code` required max 20, `string CodeType` required — `icd10` or `cpt`); `ManualCodeSelectionRequestDto` (`Guid PatientId`, `string Code`, `string CodeType`, `string Description`).
2. **Create `ICodeReferenceRepository` / `CodeReferenceRepository`**: Execute raw SQL UNION with pg_trgm: `SELECT code, description, 'icd10' AS code_type, is_deprecated, similarity(code || ' ' || description, :q) AS score FROM icd_codes WHERE (code ILIKE :pattern OR description ILIKE :pattern) [AND is_deprecated = false] UNION ALL SELECT code, description, 'cpt' AS code_type, is_deprecated, similarity(...) FROM cpt_codes WHERE ... ORDER BY score DESC LIMIT :limit`. Apply `type` filter to skip irrelevant UNION branch. `includeDeprecated = false` adds `AND is_deprecated = false` to both branches (Edge Case 2).
3. **Create `ICodeFavoriteRepository` / `CodeFavoriteRepository`**: `GetByUserAsync(Guid userId)` → query `user_code_favorites` joined with `icd_codes`/`cpt_codes` for description. `AddAsync(Guid userId, string code, string codeType)` → check code exists in reference table (HTTP 422 if not); insert row. `RemoveAsync(Guid userId, string codeType, string code)` → delete; return `bool` indicating whether row existed (HTTP 404 if `false`).
4. **Create `ICodeSearchService` / `CodeSearchService`**: `SearchAsync(q, type, includeDeprecated, userId, limit)`: check Redis cache (`codes:search:{hash(q+type+includeDeprecated)}`); on miss → call `CodeReferenceRepository.SearchAsync` → map to `CodeResultDto` list → fetch user's favorites → set `IsFavorited` on matching items → pin favorites to top of results → cache the base result (sans per-user favorited flags) for 60s → return enriched `CodeSearchResponseDto` (Edge Case 1: empty results → HTTP 200 `{ results: [], totalCount: 0 }`).
5. **Create `CodeSearchController`**: `[HttpGet("codes/search")]` — `[Authorize(Roles = "Clinician")]`; bind query params; validate `q.Length >= 2` (HTTP 400 if not); validate `type` enum value; call service; return 200. `[HttpGet("users/me/code-favorites")]` — returns user's favorites list. `[HttpPost("users/me/code-favorites")]` — validates body; HTTP 422 if code not found in reference; HTTP 201 on success. `[HttpDelete("users/me/code-favorites/{codeType}/{code}")]` — HTTP 204 on success; HTTP 404 if not in favorites.
6. **Add `POST /api/v1/coding-decisions/manual`** to `CodingDecisionController` (US_051): `[Authorize(Roles = "Clinician")]`; bind `ManualCodeSelectionRequestDto`; call `CodingDecisionRepository.InsertPendingAsync` with `reviewer_action = accepted` (manual selection is immediately finalized); write audit record `event_type = "coding_manual_selected"` via `AuditService`; return HTTP 201 with new `decisionId`.
7. **OpenTelemetry instrumentation**: `code_search.query_duration_ms` histogram per search request; `code_favorite.add_count` and `code_favorite.remove_count` counters. Tags: `query.type`, `results.count`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Controllers/
│   │   │   ├── CodingDecisionController.cs       ← MODIFY (add POST /coding-decisions/manual)
│   │   │   └── CodeSearchController.cs           ← CREATE
│   │   ├── Services/
│   │   │   ├── ICodeSearchService.cs             ← CREATE
│   │   │   └── CodeSearchService.cs              ← CREATE
│   │   ├── Repositories/
│   │   │   ├── ICodeReferenceRepository.cs       ← CREATE
│   │   │   ├── CodeReferenceRepository.cs        ← CREATE (raw SQL UNION + pg_trgm)
│   │   │   ├── ICodeFavoriteRepository.cs        ← CREATE
│   │   │   └── CodeFavoriteRepository.cs         ← CREATE
│   │   ├── DTOs/
│   │   │   ├── CodeResultDto.cs                  ← CREATE
│   │   │   ├── CodeSearchResponseDto.cs          ← CREATE
│   │   │   ├── AddFavoriteRequestDto.cs          ← CREATE
│   │   │   └── ManualCodeSelectionRequestDto.cs  ← CREATE
│   │   └── [existing module structure...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Controllers/CodeSearchController.cs` | GET search; GET/POST/DELETE favorites; Clinician-only |
| CREATE | `Modules/ClinicalIntelligence/Services/ICodeSearchService.cs` | Search + favorites service interface |
| CREATE | `Modules/ClinicalIntelligence/Services/CodeSearchService.cs` | Trigram UNION search; per-user favorites merge; Redis 60s cache; deprecated filter (Edge Case 2) |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ICodeReferenceRepository.cs` | SearchAsync interface for icd_codes + cpt_codes UNION |
| CREATE | `Modules/ClinicalIntelligence/Repositories/CodeReferenceRepository.cs` | Raw SQL UNION with pg_trgm similarity scoring; type and deprecated filters |
| CREATE | `Modules/ClinicalIntelligence/Repositories/ICodeFavoriteRepository.cs` | CRUD interface for user_code_favorites |
| CREATE | `Modules/ClinicalIntelligence/Repositories/CodeFavoriteRepository.cs` | EF Core; code existence validation; join for description |
| CREATE | `Modules/ClinicalIntelligence/DTOs/CodeResultDto.cs` | Code, Description, CodeType, IsDeprecated, IsFavorited |
| CREATE | `Modules/ClinicalIntelligence/DTOs/CodeSearchResponseDto.cs` | Results list + totalCount |
| CREATE | `Modules/ClinicalIntelligence/DTOs/AddFavoriteRequestDto.cs` | Code (max 20 required), CodeType (icd10/cpt required) |
| CREATE | `Modules/ClinicalIntelligence/DTOs/ManualCodeSelectionRequestDto.cs` | PatientId, Code, CodeType, Description |
| MODIFY | `Modules/ClinicalIntelligence/Controllers/CodingDecisionController.cs` | Add POST /coding-decisions/manual; audit coding_manual_selected |

---

## External References

- PostgreSQL pg_trgm: https://www.postgresql.org/docs/current/pgtrgm.html
- EF Core raw SQL: https://learn.microsoft.com/en-us/ef/core/querying/sql-queries
- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- NFR-002: API responses ≤ 500ms p95 — trigram GIN indexes + Redis cache required
- FR-MC-004 [DETERMINISTIC]: Code search with autocomplete and favorites — no AI pipeline
- Edge Case 2: Deprecated codes excluded by default; `includeDeprecated=true` param overrides

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

- [ ] `GET /api/v1/codes/search?q=diab&type=icd10` returns matching ICD-10 codes within 500ms p95 (AC-1, NFR-002)
- [ ] By default, `is_deprecated = true` codes excluded; `?includeDeprecated=true` includes them (Edge Case 2)
- [ ] Zero results returns HTTP 200 `{ results: [], totalCount: 0 }` (Edge Case 1)
- [ ] Favorites appear at the top of results; `isFavorited: true` set on favorited codes (AC-3)
- [ ] `POST /api/v1/users/me/code-favorites` with non-existent code returns HTTP 422
- [ ] `DELETE /api/v1/users/me/code-favorites/{type}/{code}` for non-favorited code returns HTTP 404 (AC-4)
- [ ] `POST /api/v1/coding-decisions/manual` inserts `reviewer_action = accepted` row; audit record `coding_manual_selected` written (AC-2)
- [ ] Redis cache served on second identical search; cache miss metrics emitted
- [ ] `code_search.query_duration_ms` histogram emitted per request; `code_favorite.add_count` counter increments on add

---

## Implementation Checklist

- [x] Create `CodeResultDto`, `CodeSearchResponseDto`, `AddFavoriteRequestDto`, `ManualCodeSelectionRequestDto` DTOs
- [x] Create `ICodeReferenceRepository` / `CodeReferenceRepository`: raw SQL UNION pg_trgm similarity; type filter; deprecated filter (Edge Case 2); GIN index dependency on task_003
- [x] Create `ICodeFavoriteRepository` / `CodeFavoriteRepository`: CRUD on `user_code_favorites`; code existence validation (HTTP 422 guard); join for descriptions
- [x] Create `ICodeSearchService` / `CodeSearchService`: UNION search, favorites merge (pinned first), Redis 60s cache; HTTP 200 on empty (Edge Case 1)
- [x] Create `CodeSearchController`: GET search (min q=2 validation), GET/POST/DELETE favorites (Clinician-only)
- [x] Modify `CodingDecisionController`: add POST /coding-decisions/manual with `reviewer_action = accepted` + `AuditService` write (AC-2)
- [x] Register all new services/repositories in DI; add OpenTelemetry metrics

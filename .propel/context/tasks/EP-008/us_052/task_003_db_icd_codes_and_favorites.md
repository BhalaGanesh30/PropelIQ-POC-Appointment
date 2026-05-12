---
task_id: task_003
user_story: us_052
epic: EP-008
layer: Database
status: completed
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_052] Code Search with Autocomplete and Favorites
- **Story Location**: [.propel/context/tasks/EP-008/us_052/us_052.md](.propel/context/tasks/EP-008/us_052/us_052.md)
- **Acceptance Criteria**:
  - AC-1: Given I type ≥ 2 characters, Then matching codes are returned within 500ms — `icd_codes` requires a GIN trigram index (`pg_trgm`) on `code || ' ' || description` to meet NFR-002.
  - AC-3: Given I click "Favorite", Then the code is persisted to my personal favorites — requires `user_code_favorites` table.
  - AC-4: Given I click "Unfavorite", Then the change is persisted immediately — `user_code_favorites` DELETE operation.
- **Edge Cases**:
  - Edge Case 1: No results — empty result set from UNION query; no special schema handling required.
  - Edge Case 2: Deprecated codes — `icd_codes.is_deprecated` column controls filter behaviour (parallel to `cpt_codes.is_deprecated` from US_050/task_003).

---

## Design References (Database Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A (database task) |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | N/A | N/A |
| ORM | Entity Framework Core (migrations) | 8.x |
| Database | PostgreSQL 15.x (pg_trgm extension) | 15.x |
| Cache | N/A | N/A |
| Observability | N/A | N/A |
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

Create two new tables and enable the `pg_trgm` extension via an EF Core migration:

1. **`icd_codes`** — ICD-10 reference catalog (mirrors `cpt_codes` structure from US_050/task_003). Columns: `icd_code` (string PK), `description`, `category`, `is_deprecated`, `effective_date`, `deprecation_date`, `last_updated_at`. GIN trigram index on `(icd_code || ' ' || description)` is critical for the 500ms NFR-002 search target (AC-1).

2. **`user_code_favorites`** — per-user favorites list. Composite PK on `(user_id, code_type, code)` ensuring each user can favorite a code once. `code_type_enum` (`icd10` / `cpt`); `user_id` FK to `users`; no FK to the code reference tables (codes may span both `icd_codes` and `cpt_codes`; integrity enforced at service layer).

Also enable `CREATE EXTENSION IF NOT EXISTS pg_trgm` in the migration — required by both `icd_codes` and `cpt_codes` GIN indexes.

---

## Dependent Tasks

- No upstream table dependencies — `icd_codes` is standalone; `user_code_favorites` FK only references `users`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `pg_trgm` extension | ENABLE | `CREATE EXTENSION IF NOT EXISTS pg_trgm` — required for GIN trigram index |
| `code_type_enum` | CREATE | PostgreSQL enum: `icd10`, `cpt` |
| `icd_codes` table | CREATE | ICD-10 reference catalog; string PK; is_deprecated; last_updated_at |
| `ix_icd_codes_trgm` | CREATE | GIN index on `(icd_code \|\| ' ' \|\| description) gin_trgm_ops` — enables ≤500ms ILIKE / similarity queries (AC-1, NFR-002) |
| `ix_icd_codes_active` | CREATE | Partial B-tree on `icd_code WHERE is_deprecated = false` (Edge Case 2) |
| `user_code_favorites` table | CREATE | Composite PK (user_id, code_type, code); FK to users ON DELETE CASCADE |
| `ix_user_code_favorites_user` | CREATE | B-tree on `user_id` for fast per-user lookup (AC-3, AC-4) |
| EF Core `IcdCodeEntity` | CREATE | Maps `icd_codes`; string natural PK `IcdCode`; mirrors `CptCodeEntity` structure |
| EF Core `UserCodeFavoriteEntity` | CREATE | Maps `user_code_favorites`; composite PK (`UserId`, `CodeType`, `Code`) |
| EF Core migration | CREATE | `AddIcdCodesAndUserFavorites` — pg_trgm extension + icd_codes + user_code_favorites + indexes |

---

## Implementation Plan

1. **Enable `pg_trgm`**: In the migration `Up()` method: `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;")`. Safe to run multiple times. Required before GIN index creation.
2. **Create `code_type_enum`**: PostgreSQL enum `icd10`, `cpt`. Map to C# enum `CodeType { Icd10, Cpt }` via `HasPostgresEnum` in DbContext.
3. **Create `icd_codes` table**: Columns: `icd_code VARCHAR(20) PRIMARY KEY`, `description TEXT NOT NULL`, `category VARCHAR(100) NULL`, `is_deprecated BOOLEAN NOT NULL DEFAULT false`, `effective_date DATE NULL`, `deprecation_date DATE NULL`, `last_updated_at TIMESTAMPTZ NOT NULL DEFAULT now()`. Structure mirrors `cpt_codes` (US_050/task_003) for UNION query consistency.
4. **Add `icd_codes` indexes**: GIN trigram: `CREATE INDEX ix_icd_codes_trgm ON icd_codes USING GIN ((icd_code || ' ' || description) gin_trgm_ops)` — enables ILIKE and `similarity()` within 500ms on large catalogs (AC-1, NFR-002). Partial B-tree: `CREATE INDEX ix_icd_codes_active ON icd_codes(icd_code) WHERE is_deprecated = false` (Edge Case 2). Note: `cpt_codes` GIN trigram index should be added in the same migration if not already present (US_050/task_003 did not include one — add it here via `migrationBuilder.Sql()`).
5. **Create `user_code_favorites` table**: Columns: `user_id UUID NOT NULL REFERENCES users(user_id) ON DELETE CASCADE`, `code_type code_type_enum NOT NULL`, `code VARCHAR(20) NOT NULL`, `created_at TIMESTAMPTZ NOT NULL DEFAULT now()`. Primary key: `(user_id, code_type, code)`.
6. **Add `user_code_favorites` index**: `CREATE INDEX ix_user_code_favorites_user ON user_code_favorites(user_id)` — fast per-user favorites lookup.
7. **Create `IcdCodeEntity`**: Properties mirror `CptCodeEntity` — `string IcdCode` (string PK with `HasKey`/no value generation), `string Description`, `string? Category`, `bool IsDeprecated`, `DateOnly? EffectiveDate`, `DateOnly? DeprecationDate`, `DateTimeOffset LastUpdatedAt`.
8. **Create `UserCodeFavoriteEntity`**: Properties: `Guid UserId`, `CodeType CodeType`, `string Code`, `DateTimeOffset CreatedAt`. Configure composite PK via `HasKey(e => new { e.UserId, e.CodeType, e.Code })`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── Entities/
│   │   │   │   ├── CptCodeEntity.cs                    ← EXISTS (US_050)
│   │   │   │   ├── CodingDecisionEntity.cs             ← EXISTS (US_049)
│   │   │   │   ├── IcdCodeEntity.cs                    ← CREATE
│   │   │   │   └── UserCodeFavoriteEntity.cs           ← CREATE
│   │   │   ├── Migrations/
│   │   │   │   ├── AddCptCodesReferenceTable.cs        ← EXISTS (US_050)
│   │   │   │   └── AddIcdCodesAndUserFavorites.cs      ← CREATE
│   │   │   └── [existing DbContext...]
│   └── [existing modules...]
Database:
├── cpt_codes                                           ← EXISTS (US_050)
├── coding_decisions                                    ← EXISTS (US_049)
├── icd_codes                                           ← CREATE (this task)
└── user_code_favorites                                 ← CREATE (this task)
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Entities/IcdCodeEntity.cs` | EF Core entity for icd_codes; string natural PK; mirrors CptCodeEntity structure |
| CREATE | `Modules/ClinicalIntelligence/Data/Entities/UserCodeFavoriteEntity.cs` | EF Core entity for user_code_favorites; composite PK (UserId, CodeType, Code) |
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/AddIcdCodesAndUserFavorites.cs` | pg_trgm enable + code_type_enum + icd_codes table + ix_icd_codes_trgm (GIN) + ix_icd_codes_active + cpt_codes GIN index + user_code_favorites table + ix_user_code_favorites_user |

---

## External References

- PostgreSQL pg_trgm: https://www.postgresql.org/docs/current/pgtrgm.html
- EF Core composite primary keys: https://learn.microsoft.com/en-us/ef/core/modeling/keys#composite-keys
- EF Core DateOnly (Npgsql): https://www.npgsql.org/efcore/mapping/datetime.html
- Npgsql PostgreSQL enums: https://www.npgsql.org/efcore/mapping/enum.html
- NFR-002: 500ms p95 API response — GIN trigram index is the primary enabler for code search
- FR-MC-004 [DETERMINISTIC]: Code search with autocomplete and favorites

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddIcdCodesAndUserFavorites --project src/Modules/ClinicalIntelligence

# Apply migration to database
dotnet ef database update --project src/Modules/ClinicalIntelligence

# Verify migration applied
dotnet ef migrations list --project src/Modules/ClinicalIntelligence
```

---

## Implementation Validation Strategy

- [ ] `pg_trgm` extension enabled (verified via `SELECT * FROM pg_extension WHERE extname = 'pg_trgm'`)
- [ ] `icd_codes` table created with all columns; string PK `icd_code`; `is_deprecated DEFAULT false`, `last_updated_at DEFAULT now()`
- [ ] GIN index `ix_icd_codes_trgm` exists on `(icd_code || ' ' || description) gin_trgm_ops` — ILIKE query on 100k+ rows completes within 500ms (AC-1, NFR-002)
- [ ] Partial index `ix_icd_codes_active` exists on `icd_code WHERE is_deprecated = false` (Edge Case 2)
- [ ] `user_code_favorites` composite PK `(user_id, code_type, code)` prevents duplicate favorites per user
- [ ] FK `user_id → users(user_id) ON DELETE CASCADE` — user deletion removes their favorites
- [ ] `code_type_enum` contains exactly `icd10` and `cpt`
- [ ] Migration is additive only; `dotnet ef database update` succeeds without errors on an existing DB with prior migrations

---

## Implementation Checklist

- [x] Create `IcdCodeEntity` with string natural PK and nullable deprecation/date fields; mirrors `CptCodeEntity` structure (US_050)
- [x] Create `UserCodeFavoriteEntity` with composite PK `(UserId, CodeType, Code)`; `CodeType` mapped as varchar (not PG enum) for simplicity
- [x] Create EF Core migration `AddCodeSearchSchema`: `pg_trgm` enable; `icd_codes` table; `ix_icd_codes_trgm` GIN expression index on `(code || ' ' || description)`; `ix_icd_codes_active` partial index; `ix_icd_codes_last_updated` B-tree; `cpt_codes` GIN trigram index (backfill); `user_code_favorites` table; `ix_user_code_favorites_user_id` index
- [x] Verify no FK from `user_code_favorites.code` to reference tables (integrity at service layer, not DB constraint)

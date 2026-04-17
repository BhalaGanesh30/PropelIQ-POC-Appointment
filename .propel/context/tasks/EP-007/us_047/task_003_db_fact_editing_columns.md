---
task_id: task_003
user_story: us_047
epic: EP-007
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_047] Authorized Data Editing and Verification
- **Story Location**: [.propel/context/tasks/EP-007/us_047/us_047.md](.propel/context/tasks/EP-007/us_047/us_047.md)
- **Acceptance Criteria**:
  - AC-1/AC-2: Edit and verify operations must persist `verified_by` and a timestamp — requires `verified_at TIMESTAMPTZ` column (the `verified_by` UUID FK already exists from US_044/task_002).
  - AC-3: Audit trail (previous values, editors, timestamps) uses the existing `audit_records` table — no new table required.
  - Edge Case 1: Optimistic concurrency requires `row_version INTEGER` column on `clinical_facts` for ETag-based conflict detection.
- **Edge Cases**:
  - Edge Case 1: Concurrent edits detected via `row_version` mismatch — the atomic `UPDATE … WHERE row_version = @expected` pattern depends on this column existing.
  - Edge Case 2: No DB change required for coding decision warning — `coding_decisions.fact_id` FK already planned in the data model.

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
| Database | PostgreSQL | 15.x |
| ORM | Entity Framework Core | 8.x |
| Migration Tool | EF Core Migrations | 8.x |
| Backend | ASP.NET Core | 8.x |
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

Add two columns to the existing `clinical_facts` table via an additive EF Core migration:

1. **`row_version INTEGER NOT NULL DEFAULT 1`** — monotonically incrementing integer used as an optimistic concurrency token. The application-layer `UpdateAsync` pattern atomically increments this with `WHERE row_version = @expected`, returning rows affected to detect conflicts (Edge Case 1, DR-002).

2. **`verified_at TIMESTAMPTZ NULL`** — records the timestamp at which a clinician verified or edited the fact. Complements the existing `verified_by` FK column (added in US_044/task_002) to satisfy DR-003 (last reviewer metadata) and AC-2.

3. **`updated_at TIMESTAMPTZ NULL`** — records the last modification timestamp for the row. Populated by the application layer on each PATCH or verify operation.

No new tables are required. Audit history is stored in the existing `audit_records` table (with `entity_type = 'clinical_fact'`).

---

## Dependent Tasks

- **us_044/task_002** — `clinical_facts` table base schema must exist for this additive migration to apply.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `clinical_facts` table | MODIFY | Add `row_version`, `verified_at`, `updated_at` columns |
| `ix_clinical_facts_row_version` index | CREATE | B-tree on `(fact_id, row_version)` to support efficient atomic update-with-check pattern |
| EF Core migration file | CREATE | `AddFactEditingColumns` migration |
| `ClinicalIntelligenceDbContext` | MODIFY | Update `ClinicalFact` entity configuration with new columns |
| `ClinicalFact` (EF entity) | MODIFY | Add `RowVersion`, `VerifiedAt`, `UpdatedAt` properties |

---

## Implementation Plan

1. **Add columns** via EF Core migration raw SQL:
   ```sql
   ALTER TABLE clinical_facts
       ADD COLUMN row_version  INTEGER      NOT NULL DEFAULT 1,
       ADD COLUMN verified_at  TIMESTAMPTZ  NULL,
       ADD COLUMN updated_at   TIMESTAMPTZ  NULL;
   ```
2. **Create supporting index**:
   ```sql
   CREATE INDEX ix_clinical_facts_row_version
       ON clinical_facts (fact_id, row_version);
   ```
3. **Update `ClinicalFact` EF entity**: Add `public int RowVersion { get; set; }`, `public DateTimeOffset? VerifiedAt { get; set; }`, `public DateTimeOffset? UpdatedAt { get; set; }`. Do NOT configure `RowVersion` as an EF Core concurrency token (`IsConcurrencyToken()`) — the atomic `UPDATE … WHERE row_version = @expected` pattern is implemented directly in `ClinicalFactRepository.UpdateAsync()` via `ExecuteSqlRawAsync` to avoid EF Core's concurrency exception path, which differs from the desired HTTP 409 custom response.
4. **Update `ClinicalIntelligenceDbContext.OnModelCreating()`**: Configure `RowVersion` with `HasDefaultValue(1)`, `verified_at` and `updated_at` as nullable with `HasColumnType("timestamptz")`.
5. **Generate migration**: `dotnet ef migrations add AddFactEditingColumns --project src/Modules/ClinicalIntelligence --startup-project src/Api`.
6. **Down migration**: `DROP INDEX ix_clinical_facts_row_version; ALTER TABLE clinical_facts DROP COLUMN row_version, DROP COLUMN verified_at, DROP COLUMN updated_at;`

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── ClinicalIntelligenceDbContext.cs      ← MODIFY (update ClinicalFact entity config)
│   │   │   └── Migrations/
│   │   │       ├── YYYYMMDDHHMMSS_AddClinicalDocumentsTable.cs         ← EXISTS (US_040)
│   │   │       ├── YYYYMMDDHHMMSS_AddOcrSupport.cs                     ← EXISTS (US_041)
│   │   │       ├── YYYYMMDDHHMMSS_AddFullTextSearchIndex.cs            ← EXISTS (US_042)
│   │   │       ├── YYYYMMDDHHMMSS_AddDocumentManagementColumns.cs      ← EXISTS (US_043)
│   │   │       ├── YYYYMMDDHHMMSS_AddClinicalFactsTable.cs             ← EXISTS (US_044)
│   │   │       ├── YYYYMMDDHHMMSS_AddConflictDetectionTables.cs        ← EXISTS (US_046)
│   │   │       └── YYYYMMDDHHMMSS_AddFactEditingColumns.cs             ← CREATE
│   │   └── Entities/
│   │       ├── ClinicalFact.cs       ← MODIFY (add RowVersion, VerifiedAt, UpdatedAt properties)
│   │       └── [existing entities...]
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddFactEditingColumns.cs` | Additive migration: add row_version, verified_at, updated_at to clinical_facts; add index |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Update ClinicalFact entity configuration for new columns |
| MODIFY | `Modules/ClinicalIntelligence/Entities/ClinicalFact.cs` | Add RowVersion (int), VerifiedAt (DateTimeOffset?), UpdatedAt (DateTimeOffset?) properties |

---

## External References

- PostgreSQL ALTER TABLE: https://www.postgresql.org/docs/current/sql-altertable.html
- EF Core raw SQL: https://learn.microsoft.com/en-us/ef/core/querying/sql-queries
- Optimistic concurrency without EF tokens: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- DR-003: Clinical fields must store verification state and last reviewer metadata
- NFR-010: Immutable audit evidence — audit_records table captures change history; row_version tracks current state only

---

## Build Commands

```bash
# Generate EF Core migration
dotnet ef migrations add AddFactEditingColumns \
  --project src/Modules/ClinicalIntelligence \
  --startup-project src/Api

# Apply migration
dotnet ef database update \
  --project src/Modules/ClinicalIntelligence \
  --startup-project src/Api

# Verify migration SQL
dotnet ef migrations script \
  --project src/Modules/ClinicalIntelligence \
  --startup-project src/Api
```

---

## Implementation Validation Strategy

- [ ] Migration applies successfully to existing `clinical_facts` table without data loss
- [ ] `row_version INTEGER NOT NULL DEFAULT 1` column exists; all existing rows have `row_version = 1`
- [ ] `verified_at TIMESTAMPTZ NULL` column exists; existing rows have `NULL`
- [ ] `updated_at TIMESTAMPTZ NULL` column exists; existing rows have `NULL`
- [ ] `ix_clinical_facts_row_version` index on `(fact_id, row_version)` exists
- [ ] `ClinicalFact` EF entity has `RowVersion`, `VerifiedAt`, `UpdatedAt` properties mapped correctly
- [ ] Down migration removes the three columns and the index cleanly without errors
- [ ] EF Core model snapshot is consistent with the migration

---

## Implementation Checklist

- [ ] Add `row_version INTEGER NOT NULL DEFAULT 1` to `clinical_facts` via `ALTER TABLE`
- [ ] Add `verified_at TIMESTAMPTZ NULL` and `updated_at TIMESTAMPTZ NULL` to `clinical_facts`
- [ ] Create `ix_clinical_facts_row_version` index on `(fact_id, row_version)`
- [ ] Add `RowVersion`, `VerifiedAt`, `UpdatedAt` properties to `ClinicalFact` EF entity
- [ ] Update `ClinicalIntelligenceDbContext.OnModelCreating()` with new column configurations

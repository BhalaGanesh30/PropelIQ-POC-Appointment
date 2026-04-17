---
task_id: task_003
user_story: us_043
epic: EP-006
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_043] Document Categorization, Rename, and Soft-Delete
- **Story Location**: [.propel/context/tasks/EP-006/us_043/us_043.md](.propel/context/tasks/EP-006/us_043/us_043.md)
- **Acceptance Criteria**:
  - AC-1: Given I have access to a patient's document list, When I assign a category to a document, Then the category is saved — requires `category` column typed as enum.
  - AC-2: Given I view a document in the document list, When I rename it, Then the display name is updated — requires `display_name` column separate from storage filename.
  - AC-3: Given I want to remove a document from view, When I soft-delete a document, Then the document is hidden with `IsDeleted = true` — requires `is_deleted` and `deleted_at` columns.
  - AC-4: Given I am an admin reviewing soft-deleted documents, When I access the trash view, Then soft-deleted documents are listed with their deletion date — requires `deleted_at` timestamp.
- **Edge Cases**:
  - Edge Case 1: Categorizing a document still processing — `category` column is independent of `extraction_status`; no database constraint links them.
  - Edge Case 2: Hard deletion prevention — no cascade delete; `ON DELETE RESTRICT` on FK prevents accidental data loss.

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

Extend the `clinical_documents` table (created in US_040/task_003) with columns required for document management: `display_name` for user-facing rename (AC-2), `is_deleted` and `deleted_at` for soft-delete (AC-3, AC-4), and a typed `document_category_type` PostgreSQL enum to replace the existing generic `category` column with a constrained set of values (AC-1). The migration also adds a partial index on `is_deleted` for efficient active-document queries and a composite index on `(patient_id, is_deleted)` for the filtered listing endpoint. The existing `category VARCHAR(50) NULL` column from US_040/task_003 is altered to use the new `document_category_type` enum. All changes are backward-compatible via EF Core migration with a reversible `Down()` method.

---

## Dependent Tasks

- **us_040/task_003** — `clinical_documents` table must exist with the base schema.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `document_category_type` (PostgreSQL enum) | CREATE | Values: `lab_report`, `referral`, `prescription`, `imaging`, `insurance`, `other` |
| `clinical_documents.display_name` column | CREATE | `VARCHAR(255) NULL` — nullable; when null, UI falls back to `original_filename` |
| `clinical_documents.is_deleted` column | CREATE | `BOOLEAN NOT NULL DEFAULT false` |
| `clinical_documents.deleted_at` column | CREATE | `TIMESTAMPTZ NULL` |
| `clinical_documents.category` column | ALTER | Change from `VARCHAR(50)` to `document_category_type` enum |
| `ix_clinical_documents_is_deleted` index | CREATE | Partial index `WHERE is_deleted = false` for active document listing |
| `ix_clinical_documents_patient_active` index | CREATE | Composite index `(patient_id, is_deleted)` for filtered patient document queries |
| EF Core migration file | CREATE | `AddDocumentManagementColumns` migration |
| `ClinicalIntelligenceDbContext` | MODIFY | Configure `document_category_type` enum mapping, new column defaults, partial index |

---

## Implementation Plan

1. **Create PostgreSQL enum type** via EF Core migration raw SQL:
   ```sql
   CREATE TYPE document_category_type AS ENUM (
       'lab_report', 'referral', 'prescription', 'imaging', 'insurance', 'other'
   );
   ```
2. **Alter `category` column** from `VARCHAR(50)` to `document_category_type`:
   ```sql
   ALTER TABLE clinical_documents
       ALTER COLUMN category TYPE document_category_type
       USING category::document_category_type;
   ```
   This cast works because existing values (if any) must match enum labels. For safety, the migration first sets any unrecognized values to `NULL` before the type cast.
3. **Add new columns** via EF Core migration:
   ```sql
   ALTER TABLE clinical_documents
       ADD COLUMN display_name VARCHAR(255) NULL,
       ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT false,
       ADD COLUMN deleted_at TIMESTAMPTZ NULL;
   ```
4. **Create indexes**:
   ```sql
   CREATE INDEX ix_clinical_documents_is_deleted
       ON clinical_documents (is_deleted)
       WHERE is_deleted = false;

   CREATE INDEX ix_clinical_documents_patient_active
       ON clinical_documents (patient_id, is_deleted)
       WHERE is_deleted = false;
   ```
5. **Configure EF Core entity** in `ClinicalIntelligenceDbContext.OnModelCreating()`:
   - Register `document_category_type` PostgreSQL enum via Npgsql `HasPostgresEnum`.
   - Map `Category` property to `document_category_type` enum.
   - Configure `DisplayName` as `VARCHAR(255)` nullable.
   - Configure `IsDeleted` with `HasDefaultValue(false)`.
   - Configure `DeletedAt` as nullable `TIMESTAMPTZ`.
   - Add partial index configuration via `HasIndex(d => d.IsDeleted).HasFilter("is_deleted = false")`.
   - Add composite index via `HasIndex(d => new { d.PatientId, d.IsDeleted }).HasFilter("is_deleted = false")`.
6. **Generate migration**: Run `dotnet ef migrations add AddDocumentManagementColumns --project src/Modules/ClinicalIntelligence --startup-project src/Api`.
7. **Ensure Down migration** drops columns, indexes, and enum type in reverse order. Revert `category` column back to `VARCHAR(50)`.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── ClinicalIntelligenceDbContext.cs      ← MODIFY (enum mapping, column config, indexes)
│   │   │   └── Migrations/
│   │   │       ├── YYYYMMDDHHMMSS_AddClinicalDocumentsTable.cs     ← EXISTS (US_040/task_003)
│   │   │       ├── YYYYMMDDHHMMSS_AddOcrSupport.cs                 ← EXISTS (US_041/task_002)
│   │   │       ├── YYYYMMDDHHMMSS_AddFullTextSearchIndex.cs        ← EXISTS (US_042/task_002)
│   │   │       └── YYYYMMDDHHMMSS_AddDocumentManagementColumns.cs  ← CREATE
│   │   └── Entities/
│   │       └── ClinicalDocument.cs                   ← Modified in task_002 (adds properties)
│   └── [existing modules...]
└── [existing project structure...]
```

> Placeholder: Update this tree after the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddDocumentManagementColumns.cs` | Migration: create enum, alter category, add display_name/is_deleted/deleted_at, create indexes |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Register `document_category_type` enum, configure new columns and indexes |

---

## External References

- Npgsql PostgreSQL enum mapping: https://www.npgsql.org/efcore/mapping/enum.html
- EF Core partial indexes: https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-filter
- PostgreSQL ALTER COLUMN TYPE with USING: https://www.postgresql.org/docs/15/sql-altertable.html
- PostgreSQL partial indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- FR-DM-004: System MUST support document categorization, rename, and soft-delete operations
- CLINICAL_DOCUMENT data model: [models.md](.propel/context/docs/models.md) — lines 360-370

---

## Build Commands

```bash
# Generate EF Core migration
dotnet ef migrations add AddDocumentManagementColumns \
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

- [ ] Migration applies successfully against a PostgreSQL 15.x instance with existing `clinical_documents` table
- [ ] `document_category_type` enum exists with values: `lab_report`, `referral`, `prescription`, `imaging`, `insurance`, `other`
- [ ] `category` column type changed from `VARCHAR(50)` to `document_category_type`
- [ ] `display_name` column exists as `VARCHAR(255) NULL`
- [ ] `is_deleted` column exists as `BOOLEAN NOT NULL DEFAULT false`
- [ ] `deleted_at` column exists as `TIMESTAMPTZ NULL`
- [ ] `ix_clinical_documents_is_deleted` partial index exists (filtered `WHERE is_deleted = false`)
- [ ] `ix_clinical_documents_patient_active` composite index exists on `(patient_id, is_deleted)` filtered
- [ ] Down migration reverts all changes: drops columns, indexes, reverts `category` to `VARCHAR(50)`, drops enum
- [ ] Existing data in `clinical_documents` is preserved after migration
- [ ] EF Core model snapshot is consistent with migration

---

## Implementation Checklist

- [ ] Create `document_category_type` PostgreSQL enum via migration raw SQL
- [ ] Alter `category` column from `VARCHAR(50)` to `document_category_type` enum with safe USING cast
- [ ] Add `display_name` (`VARCHAR(255) NULL`), `is_deleted` (`BOOLEAN NOT NULL DEFAULT false`), `deleted_at` (`TIMESTAMPTZ NULL`) columns
- [ ] Create partial index `ix_clinical_documents_is_deleted` and composite index `ix_clinical_documents_patient_active`
- [ ] Configure EF Core entity with enum mapping, column defaults, and index filters in `OnModelCreating`
- [ ] Generate and verify migration; ensure Down migration reverses all changes cleanly

---
task_id: task_003
user_story: us_040
epic: EP-006
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_040] Document Upload with Malware Scanning
- **Story Location**: [.propel/context/tasks/EP-006/us_040/us_040.md](.propel/context/tasks/EP-006/us_040/us_040.md)
- **Acceptance Criteria**:
  - AC-1: Given I am authenticated, When I select and submit a file for upload, Then the system validates the file type (PDF, JPG, PNG, TIFF) and size (max 10 MB) before accepting the file.
  - AC-2: Given a valid file is submitted, When the malware scan is executed, Then the scan completes before the file is persisted and a clean file is stored in encrypted cloud storage.
  - AC-3: Given the malware scan detects a threat, When the scan result is returned, Then the file is rejected, not persisted, the upload response returns an error message, and the event is logged.
  - AC-4: Given a file type not in the approved list is submitted, When the type validation runs, Then the API returns HTTP 400 with a message listing the accepted file types.
- **Edge Cases**:
  - Edge Case 1: Malware scanner unavailable — `scan_result` set to `pending_scan`; file is not accessible until scan completes.
  - Edge Case 2: File exceeds 10 MB — rejected at API layer; no database record created.

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

Create the `clinical_documents` table and supporting enum types in PostgreSQL via EF Core migration. The schema stores metadata for uploaded clinical documents including the Cloudflare R2 object key, original filename, content type, file size, malware scan result, and OCR extraction status. This table is referenced by the `CLINICAL_DOCUMENT` entity in the data model (models.md) with relationships `PATIENT ||--o{ CLINICAL_DOCUMENT` and `CLINICAL_DOCUMENT ||--o{ CLINICAL_FACT`. The migration creates two PostgreSQL enum types (`scan_result_type` and `extraction_status_type`), the `clinical_documents` table with all columns, foreign key to `patients`, indexes for common query patterns (by patient, by scan result for retry service, by extraction status for OCR worker), and a `CHECK` constraint ensuring `file_size_bytes` does not exceed 10 MB (10,485,760 bytes) as a database-level guard aligned with FR-DM-001.

---

## Dependent Tasks

- None — this is the foundational schema task that task_001 and task_002 depend on.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `scan_result_type` (PostgreSQL enum) | CREATE | Values: `clean`, `threat_detected`, `pending_scan` |
| `extraction_status_type` (PostgreSQL enum) | CREATE | Values: `queued`, `processing`, `completed`, `failed` |
| `clinical_documents` table | CREATE | Full table with all columns per data model |
| `ix_clinical_documents_patient_id` index | CREATE | B-tree index on `patient_id` for patient document lookup |
| `ix_clinical_documents_scan_result` index | CREATE | B-tree index on `scan_result` for `MalwareScanRetryService` query |
| `ix_clinical_documents_extraction_status` index | CREATE | B-tree index on `extraction_status` for OCR worker query |
| `chk_clinical_documents_file_size` constraint | CREATE | `CHECK (file_size_bytes <= 10485760)` — database-level 10 MB guard |
| EF Core migration file | CREATE | `AddClinicalDocumentsTable` migration |
| `ClinicalIntelligenceDbContext` | MODIFY | Add `DbSet<ClinicalDocument>` and entity configuration |

---

## Implementation Plan

1. **Create PostgreSQL enum types** via EF Core migration raw SQL:
   ```sql
   CREATE TYPE scan_result_type AS ENUM ('clean', 'threat_detected', 'pending_scan');
   CREATE TYPE extraction_status_type AS ENUM ('queued', 'processing', 'completed', 'failed');
   ```
2. **Create `clinical_documents` table** via EF Core migration:
   ```sql
   CREATE TABLE clinical_documents (
       document_id       UUID            NOT NULL DEFAULT gen_random_uuid(),
       patient_id        UUID            NOT NULL,
       r2_object_key     VARCHAR(512)    NOT NULL,
       original_filename VARCHAR(255)    NOT NULL,
       content_type      VARCHAR(100)    NOT NULL,
       file_size_bytes   BIGINT          NOT NULL,
       category          VARCHAR(50)     NULL,
       scan_result       scan_result_type NOT NULL DEFAULT 'pending_scan',
       extraction_status extraction_status_type NOT NULL DEFAULT 'queued',
       extracted_text    TEXT            NULL,
       uploaded_at       TIMESTAMPTZ     NOT NULL DEFAULT now(),
       CONSTRAINT pk_clinical_documents PRIMARY KEY (document_id),
       CONSTRAINT fk_clinical_documents_patient
           FOREIGN KEY (patient_id) REFERENCES patients (patient_id)
           ON DELETE RESTRICT,
       CONSTRAINT chk_clinical_documents_file_size
           CHECK (file_size_bytes <= 10485760)
   );
   ```
3. **Create indexes**:
   ```sql
   CREATE INDEX ix_clinical_documents_patient_id
       ON clinical_documents (patient_id);

   CREATE INDEX ix_clinical_documents_scan_result
       ON clinical_documents (scan_result)
       WHERE scan_result = 'pending_scan';

   CREATE INDEX ix_clinical_documents_extraction_status
       ON clinical_documents (extraction_status)
       WHERE extraction_status IN ('queued', 'processing');
   ```
4. **Configure EF Core entity** in `ClinicalIntelligenceDbContext.OnModelCreating()`:
   - Map `ClinicalDocument` to `clinical_documents` table.
   - Configure `DocumentId` as PK with `HasDefaultValueSql("gen_random_uuid()")`.
   - Configure `ScanResult` property to map to `scan_result_type` PostgreSQL enum via Npgsql `HasPostgresEnum`.
   - Configure `ExtractionStatus` property to map to `extraction_status_type` PostgreSQL enum via Npgsql `HasPostgresEnum`.
   - Configure `PatientId` FK with `OnDelete(DeleteBehavior.Restrict)`.
   - Configure `HasCheckConstraint("chk_clinical_documents_file_size", "file_size_bytes <= 10485760")`.
   - Add `DbSet<ClinicalDocument> ClinicalDocuments { get; set; }`.
5. **Generate migration**: Run `dotnet ef migrations add AddClinicalDocumentsTable --project src/Modules/ClinicalIntelligence --startup-project src/Api`.
6. **Add Down migration**: Ensure `Down()` method drops table, indexes, and enum types in reverse order.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── ClinicalIntelligenceDbContext.cs      ← MODIFY (add DbSet, entity config)
│   │   │   └── Migrations/
│   │   │       └── YYYYMMDDHHMMSS_AddClinicalDocumentsTable.cs  ← CREATE
│   │   └── Entities/
│   │       └── ClinicalDocument.cs                   ← Created in task_002
│   └── [existing modules...]
└── [existing project structure...]
```

> Placeholder: Update this tree after the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddClinicalDocumentsTable.cs` | EF Core migration: create enums, table, indexes, check constraint |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Add `DbSet<ClinicalDocument>`, configure entity in `OnModelCreating` with enum mappings, FK, indexes, check constraint |

---

## External References

- Npgsql PostgreSQL enum mapping: https://www.npgsql.org/efcore/mapping/enum.html
- EF Core `HasCheckConstraint`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.relationalentitytypebuilderextensions.hascheckconstraint
- PostgreSQL `gen_random_uuid()`: https://www.postgresql.org/docs/15/functions-uuid.html
- PostgreSQL partial indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- CLINICAL_DOCUMENT data model: [models.md](.propel/context/docs/models.md) — lines 358-380
- FR-DM-001: System MUST accept PDF, JPG, PNG, and TIFF files up to 10 MB and complete malware scan before persistence

---

## Build Commands

```bash
# Generate EF Core migration
dotnet ef migrations add AddClinicalDocumentsTable \
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

- [ ] Migration applies successfully against a clean PostgreSQL 15.x instance
- [ ] `scan_result_type` enum exists with values `clean`, `threat_detected`, `pending_scan`
- [ ] `extraction_status_type` enum exists with values `queued`, `processing`, `completed`, `failed`
- [ ] `clinical_documents` table exists with all specified columns and correct data types
- [ ] `pk_clinical_documents` primary key constraint exists on `document_id`
- [ ] `fk_clinical_documents_patient` foreign key references `patients.patient_id` with `ON DELETE RESTRICT`
- [ ] `chk_clinical_documents_file_size` check constraint rejects inserts with `file_size_bytes > 10485760`
- [ ] `ix_clinical_documents_patient_id` B-tree index exists
- [ ] `ix_clinical_documents_scan_result` partial index exists (filtered on `pending_scan`)
- [ ] `ix_clinical_documents_extraction_status` partial index exists (filtered on `queued`, `processing`)
- [ ] Down migration drops table, indexes, and enum types cleanly
- [ ] EF Core model snapshot is consistent with migration

---

## Implementation Checklist

- [ ] Create `scan_result_type` and `extraction_status_type` PostgreSQL enum types via migration
- [ ] Create `clinical_documents` table with all columns: `document_id`, `patient_id`, `r2_object_key`, `original_filename`, `content_type`, `file_size_bytes`, `category`, `scan_result`, `extraction_status`, `extracted_text`, `uploaded_at`
- [ ] Add PK, FK (`patients.patient_id`, `ON DELETE RESTRICT`), and CHECK constraint (`file_size_bytes <= 10485760`)
- [ ] Create indexes: `ix_clinical_documents_patient_id`, `ix_clinical_documents_scan_result` (partial), `ix_clinical_documents_extraction_status` (partial)
- [ ] Configure EF Core entity in `ClinicalIntelligenceDbContext` with enum mappings, FK, indexes, and check constraint
- [ ] Generate and verify migration; ensure Down migration reverses all changes

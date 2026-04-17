---
task_id: task_002
user_story: us_044
epic: EP-007
layer: Database
status: not-started
effort_hours: 3
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_044] Clinical Entity Extraction with Confidence Scoring
- **Story Location**: [.propel/context/tasks/EP-007/us_044/us_044.md](.propel/context/tasks/EP-007/us_044/us_044.md)
- **Acceptance Criteria**:
  - AC-1: Given extraction runs, Then ClinicalFact records are stored with individual confidence scores — requires `clinical_facts` table with `confidence_score` column.
  - AC-2: Given an extracted fact is stored, Then source document reference is available — requires `document_id` FK column.
  - AC-3: Given confidence below threshold, Then "Low Confidence – Review Required" indicator — requires `needs_review` boolean column.
  - AC-4: Given schema validation, Then 99% of payloads pass — schema validation is application-layer; DB stores validated output.
- **Edge Cases**:
  - Edge Case 1: Low input quality — `LowInputQuality` flag stored as document-level attribute (in `clinical_documents.needs_manual_review`); no fact rows created.
  - Edge Case 2: Conflicting extractions — each extraction stored independently per `document_id`; no uniqueness constraint on (fact_type, name, patient_id).

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
| Database | PostgreSQL with pgvector | 15.x |
| ORM | Entity Framework Core | 8.x |
| Migration Tool | EF Core Migrations | 8.x |
| Vector Extension | pgvector | 0.7.x |
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
| **AIR Requirements** | N/A (schema supports AIR-001, AIR-004, but DB task itself is deterministic) |
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

Create the `clinical_facts` table and supporting `fact_type_enum` PostgreSQL enum type via EF Core migration. The table stores extracted clinical entities (medications, allergies, diagnoses, findings) produced by the AI extraction pipeline (task_001) with individual confidence scores, source document references for traceability (AIR-004), a `needs_review` flag for low-confidence items (AC-3), verification status with auditable clinician reference, and a pgvector `embedding` column for future RAG retrieval (AIR-010). The schema follows the `CLINICAL_FACT` entity in the data model (models.md) with additions for `needs_review`, `source_text`, and `embedding`. Foreign keys reference `patients` and `clinical_documents` tables. Indexes cover patient-based lookup, document-based lookup, needs-review filtering for the clinician review queue, and a HNSW vector index for similarity search.

---

## Dependent Tasks

- **us_040/task_003** — `clinical_documents` table must exist (FK target for `document_id`).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `pgvector` extension | ENABLE | `CREATE EXTENSION IF NOT EXISTS vector` |
| `fact_type_enum` (PostgreSQL enum) | CREATE | Values: `medication`, `allergy`, `diagnosis`, `finding` |
| `clinical_facts` table | CREATE | Full table with all columns per data model + extensions |
| `ix_clinical_facts_patient_id` index | CREATE | B-tree on `patient_id` for patient profile queries |
| `ix_clinical_facts_document_id` index | CREATE | B-tree on `document_id` for document-level fact listing |
| `ix_clinical_facts_needs_review` index | CREATE | Partial B-tree `WHERE needs_review = true` for clinician review queue |
| `ix_clinical_facts_embedding` index | CREATE | HNSW vector index on `embedding` for similarity search |
| EF Core migration file | CREATE | `AddClinicalFactsTable` migration |
| `ClinicalIntelligenceDbContext` | MODIFY | Add `DbSet<ClinicalFact>`, enable pgvector, configure entity |

---

## Implementation Plan

1. **Enable pgvector extension** via EF Core migration raw SQL:
   ```sql
   CREATE EXTENSION IF NOT EXISTS vector;
   ```
2. **Create PostgreSQL enum type** via migration raw SQL:
   ```sql
   CREATE TYPE fact_type_enum AS ENUM ('medication', 'allergy', 'diagnosis', 'finding');
   ```
3. **Create `clinical_facts` table** via EF Core migration:
   ```sql
   CREATE TABLE clinical_facts (
       fact_id           UUID            NOT NULL DEFAULT gen_random_uuid(),
       patient_id        UUID            NOT NULL,
       document_id       UUID            NOT NULL,
       fact_type         fact_type_enum  NOT NULL,
       name              VARCHAR(255)    NOT NULL,
       value             TEXT            NOT NULL,
       confidence_score  DECIMAL(5,4)    NOT NULL,
       needs_review      BOOLEAN         NOT NULL DEFAULT false,
       source_text       TEXT            NULL,
       verified          BOOLEAN         NOT NULL DEFAULT false,
       verified_by       UUID            NULL,
       fact_date         TIMESTAMPTZ     NULL,
       embedding         vector(1536)    NULL,
       created_at        TIMESTAMPTZ     NOT NULL DEFAULT now(),
       CONSTRAINT pk_clinical_facts PRIMARY KEY (fact_id),
       CONSTRAINT fk_clinical_facts_patient
           FOREIGN KEY (patient_id) REFERENCES patients (patient_id)
           ON DELETE RESTRICT,
       CONSTRAINT fk_clinical_facts_document
           FOREIGN KEY (document_id) REFERENCES clinical_documents (document_id)
           ON DELETE RESTRICT,
       CONSTRAINT fk_clinical_facts_verified_by
           FOREIGN KEY (verified_by) REFERENCES users (user_id)
           ON DELETE SET NULL,
       CONSTRAINT chk_clinical_facts_confidence
           CHECK (confidence_score >= 0.0 AND confidence_score <= 1.0)
   );
   ```
4. **Create indexes**:
   ```sql
   CREATE INDEX ix_clinical_facts_patient_id
       ON clinical_facts (patient_id);

   CREATE INDEX ix_clinical_facts_document_id
       ON clinical_facts (document_id);

   CREATE INDEX ix_clinical_facts_needs_review
       ON clinical_facts (needs_review)
       WHERE needs_review = true;

   CREATE INDEX ix_clinical_facts_embedding
       ON clinical_facts
       USING hnsw (embedding vector_cosine_ops)
       WITH (m = 16, ef_construction = 64);
   ```
5. **Configure EF Core entity** in `ClinicalIntelligenceDbContext.OnModelCreating()`:
   - Register `fact_type_enum` PostgreSQL enum via Npgsql `HasPostgresEnum`.
   - Map `ClinicalFact` to `clinical_facts` table.
   - Configure `FactId` as PK with `HasDefaultValueSql("gen_random_uuid()")`.
   - Configure `FactType` to map to `fact_type_enum`.
   - Configure `ConfidenceScore` as `decimal(5,4)`.
   - Configure `NeedsReview` with `HasDefaultValue(false)`.
   - Configure `Verified` with `HasDefaultValue(false)`.
   - Configure `Embedding` as `vector(1536)` using Npgsql pgvector mapping.
   - Configure FKs: `PatientId` → `patients`, `DocumentId` → `clinical_documents`, `VerifiedBy` → `users`. All with `OnDelete(DeleteBehavior.Restrict)` except `VerifiedBy` which is `SetNull`.
   - Add `HasCheckConstraint("chk_clinical_facts_confidence", "confidence_score >= 0.0 AND confidence_score <= 1.0")`.
   - Add `DbSet<ClinicalFact> ClinicalFacts { get; set; }`.
6. **Generate migration**: Run `dotnet ef migrations add AddClinicalFactsTable --project src/Modules/ClinicalIntelligence --startup-project src/Api`.
7. **Ensure Down migration** drops table, indexes, enum type, and pgvector extension (conditionally — only if no other tables use it) in reverse order.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── ClinicalIntelligenceDbContext.cs      ← MODIFY (enable pgvector, add DbSet, entity config)
│   │   │   └── Migrations/
│   │   │       ├── YYYYMMDDHHMMSS_AddClinicalDocumentsTable.cs         ← EXISTS (US_040)
│   │   │       ├── YYYYMMDDHHMMSS_AddOcrSupport.cs                     ← EXISTS (US_041)
│   │   │       ├── YYYYMMDDHHMMSS_AddFullTextSearchIndex.cs            ← EXISTS (US_042)
│   │   │       ├── YYYYMMDDHHMMSS_AddDocumentManagementColumns.cs      ← EXISTS (US_043)
│   │   │       └── YYYYMMDDHHMMSS_AddClinicalFactsTable.cs             ← CREATE
│   │   └── Entities/
│   │       ├── ClinicalDocument.cs                   ← EXISTS
│   │       └── ClinicalFact.cs                       ← Created in task_001
│   └── [existing modules...]
└── [existing project structure...]
```

> Placeholder: Update this tree after the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddClinicalFactsTable.cs` | Migration: enable pgvector, create enum, create table, create indexes (B-tree + HNSW) |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Enable pgvector, add `DbSet<ClinicalFact>`, configure entity with enum, vector column, FKs, indexes, check constraint |

---

## External References

- pgvector PostgreSQL extension: https://github.com/pgvector/pgvector
- Npgsql pgvector support: https://www.npgsql.org/efcore/mapping/other.html#pgvector
- HNSW index parameters: https://github.com/pgvector/pgvector#hnsw
- Npgsql PostgreSQL enum mapping: https://www.npgsql.org/efcore/mapping/enum.html
- text-embedding-3-small output dimensions: 1536 (default)
- CLINICAL_FACT data model: [models.md](.propel/context/docs/models.md) — lines 371-383
- AIR-004: Source citation references for extracted facts (document_id FK)
- AIR-010: Retrieval access control (embedding column supports patient-scoped RAG)

---

## Build Commands

```bash
# Generate EF Core migration
dotnet ef migrations add AddClinicalFactsTable \
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

- [ ] Migration applies successfully against a PostgreSQL 15.x instance with pgvector extension available
- [ ] `pgvector` extension enabled — `SELECT * FROM pg_extension WHERE extname = 'vector'` returns a row
- [ ] `fact_type_enum` exists with values: `medication`, `allergy`, `diagnosis`, `finding`
- [ ] `clinical_facts` table exists with all specified columns and correct data types
- [ ] `embedding` column is `vector(1536)` type
- [ ] `confidence_score` column is `DECIMAL(5,4)` with CHECK constraint (0.0–1.0)
- [ ] `pk_clinical_facts` primary key constraint exists on `fact_id`
- [ ] `fk_clinical_facts_patient` FK references `patients.patient_id` with `ON DELETE RESTRICT`
- [ ] `fk_clinical_facts_document` FK references `clinical_documents.document_id` with `ON DELETE RESTRICT`
- [ ] `fk_clinical_facts_verified_by` FK references `users.user_id` with `ON DELETE SET NULL`
- [ ] `ix_clinical_facts_patient_id` B-tree index exists
- [ ] `ix_clinical_facts_document_id` B-tree index exists
- [ ] `ix_clinical_facts_needs_review` partial index exists (filtered on `needs_review = true`)
- [ ] `ix_clinical_facts_embedding` HNSW vector index exists with `vector_cosine_ops`
- [ ] Down migration drops table, indexes, enum type cleanly
- [ ] EF Core model snapshot is consistent with migration

---

## Implementation Checklist

- [ ] Enable pgvector extension via `CREATE EXTENSION IF NOT EXISTS vector`
- [ ] Create `fact_type_enum` PostgreSQL enum with `medication`, `allergy`, `diagnosis`, `finding`
- [ ] Create `clinical_facts` table with all columns: `fact_id`, `patient_id`, `document_id`, `fact_type`, `name`, `value`, `confidence_score`, `needs_review`, `source_text`, `verified`, `verified_by`, `fact_date`, `embedding`, `created_at`
- [ ] Add PK, FKs (patients, clinical_documents, users), and CHECK constraint on `confidence_score`
- [ ] Create B-tree indexes on `patient_id`, `document_id`, partial index on `needs_review`, HNSW index on `embedding`
- [ ] Configure EF Core entity with pgvector mapping, enum mapping, FKs, and constraints in `OnModelCreating`

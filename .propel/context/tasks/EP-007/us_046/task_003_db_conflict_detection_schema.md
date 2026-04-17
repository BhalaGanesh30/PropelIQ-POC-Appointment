---
task_id: task_003
user_story: us_046
epic: EP-007
layer: Database
status: not-started
effort_hours: 3
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_046] Drug-Drug and Drug-Allergy Conflict Detection
- **Story Location**: [.propel/context/tasks/EP-007/us_046/us_046.md](.propel/context/tasks/EP-007/us_046/us_046.md)
- **Acceptance Criteria**:
  - AC-1/AC-2: Conflicts are detected and classified by severity — requires `conflict_rules` table to store drug-drug and drug-allergy interaction rules with severity levels.
  - AC-3/AC-4: Acknowledgment is recorded and audited — requires `conflict_alerts` table with `acknowledged`, `acknowledged_by`, `acknowledged_at` columns; audit written to existing `audit_records` table.
- **Edge Cases**:
  - Edge Case 1: Rules database outdated — requires `last_updated_at` column on `conflict_rules` rows to enable staleness detection.
  - Edge Case 2: Deduplication of many conflict pairs — handled at application layer; DB stores one row per detected pair (idempotent upsert).

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

Create two tables via EF Core migration to support conflict detection:

1. **`conflict_rules`** — stores the drug-drug and drug-allergy interaction rule definitions used by the detection engine. Rows are populated by a seeding script or administrative import process. The `last_updated_at` column supports staleness detection (Edge Case 1). The `is_active` flag enables rule disabling without deletion.

2. **`conflict_alerts`** — stores detected conflict instances per patient, providing stable row IDs for the `POST /api/v1/conflicts/{id}/acknowledge` endpoint (AC-3). Each row represents one unique conflict pair for a patient; idempotent upsert at the application layer prevents duplicates. Acknowledgment columns (`acknowledged`, `acknowledged_by`, `acknowledged_at`) support AC-4.

Supporting PostgreSQL enum types: `conflict_type_enum` (`drug_drug`, `drug_allergy`) and `severity_level_enum` (`low`, `moderate`, `high`, `critical`). Both tables include appropriate indexes for query patterns.

---

## Dependent Tasks

- **us_044/task_002** — `clinical_facts` table must exist (FK target for `fact_id_a` and `fact_id_b` in `conflict_alerts`).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `conflict_type_enum` (PostgreSQL enum) | CREATE | Values: `drug_drug`, `drug_allergy` |
| `severity_level_enum` (PostgreSQL enum) | CREATE | Values: `low`, `moderate`, `high`, `critical` |
| `conflict_rules` table | CREATE | Rule definitions for the detection engine |
| `conflict_alerts` table | CREATE | Per-patient detected conflict instances with acknowledgment |
| `ix_conflict_rules_type_drugs` index | CREATE | B-tree on `(rule_type, drug_a_name, drug_b_name)` for fast rule lookup |
| `ix_conflict_alerts_patient_id` index | CREATE | B-tree on `patient_id` |
| `ix_conflict_alerts_unacknowledged` index | CREATE | Partial index `WHERE acknowledged = false` |
| `uq_conflict_alerts_pair` constraint | CREATE | Unique on `(patient_id, fact_id_a, fact_id_b)` to prevent duplicate alerts |
| EF Core migration file | CREATE | `AddConflictDetectionTables` migration |
| `ClinicalIntelligenceDbContext` | MODIFY | Add `DbSet<ConflictRule>`, `DbSet<ConflictAlert>`, configure enums and entities |

---

## Implementation Plan

1. **Create PostgreSQL enum types** via migration raw SQL:
   ```sql
   CREATE TYPE conflict_type_enum AS ENUM ('drug_drug', 'drug_allergy');
   CREATE TYPE severity_level_enum AS ENUM ('low', 'moderate', 'high', 'critical');
   ```
2. **Create `conflict_rules` table**:
   ```sql
   CREATE TABLE conflict_rules (
       rule_id         UUID                NOT NULL DEFAULT gen_random_uuid(),
       rule_type       conflict_type_enum  NOT NULL,
       drug_a_name     VARCHAR(255)        NOT NULL,
       drug_b_name     VARCHAR(255)        NOT NULL,
       severity        severity_level_enum NOT NULL,
       description     TEXT                NOT NULL,
       source          VARCHAR(100)        NOT NULL DEFAULT 'system',
       is_active       BOOLEAN             NOT NULL DEFAULT true,
       last_updated_at TIMESTAMPTZ         NOT NULL DEFAULT now(),
       created_at      TIMESTAMPTZ         NOT NULL DEFAULT now(),
       CONSTRAINT pk_conflict_rules PRIMARY KEY (rule_id)
   );
   ```
3. **Create `conflict_alerts` table**:
   ```sql
   CREATE TABLE conflict_alerts (
       conflict_id      UUID                NOT NULL DEFAULT gen_random_uuid(),
       patient_id       UUID                NOT NULL,
       fact_id_a        UUID                NOT NULL,
       fact_id_b        UUID                NULL,
       conflict_type    conflict_type_enum  NOT NULL,
       severity         severity_level_enum NOT NULL,
       description      TEXT                NOT NULL,
       rule_id          UUID                NOT NULL,
       acknowledged     BOOLEAN             NOT NULL DEFAULT false,
       acknowledged_by  UUID                NULL,
       acknowledged_at  TIMESTAMPTZ         NULL,
       created_at       TIMESTAMPTZ         NOT NULL DEFAULT now(),
       CONSTRAINT pk_conflict_alerts PRIMARY KEY (conflict_id),
       CONSTRAINT fk_conflict_alerts_patient
           FOREIGN KEY (patient_id) REFERENCES patients (patient_id)
           ON DELETE RESTRICT,
       CONSTRAINT fk_conflict_alerts_fact_a
           FOREIGN KEY (fact_id_a) REFERENCES clinical_facts (fact_id)
           ON DELETE CASCADE,
       CONSTRAINT fk_conflict_alerts_fact_b
           FOREIGN KEY (fact_id_b) REFERENCES clinical_facts (fact_id)
           ON DELETE CASCADE,
       CONSTRAINT fk_conflict_alerts_rule
           FOREIGN KEY (rule_id) REFERENCES conflict_rules (rule_id)
           ON DELETE RESTRICT,
       CONSTRAINT fk_conflict_alerts_acknowledged_by
           FOREIGN KEY (acknowledged_by) REFERENCES users (user_id)
           ON DELETE SET NULL,
       CONSTRAINT uq_conflict_alerts_pair
           UNIQUE (patient_id, fact_id_a, fact_id_b)
   );
   ```
4. **Create indexes**:
   ```sql
   CREATE INDEX ix_conflict_rules_type_drugs
       ON conflict_rules (rule_type, drug_a_name, drug_b_name)
       WHERE is_active = true;

   CREATE INDEX ix_conflict_alerts_patient_id
       ON conflict_alerts (patient_id);

   CREATE INDEX ix_conflict_alerts_unacknowledged
       ON conflict_alerts (patient_id, severity)
       WHERE acknowledged = false;
   ```
5. **Configure EF Core entities** in `ClinicalIntelligenceDbContext.OnModelCreating()`:
   - Register `conflict_type_enum` and `severity_level_enum` PostgreSQL enums via `HasPostgresEnum`.
   - Map `ConflictRule` to `conflict_rules`; configure PK with `HasDefaultValueSql("gen_random_uuid()")`.
   - Map `ConflictAlert` to `conflict_alerts`; configure PK similarly.
   - Configure `ConflictAlert.FactIdB` as nullable FK.
   - Configure `AcknowledgedBy` FK to `users` with `OnDelete(DeleteBehavior.SetNull)`.
   - Configure `FactIdA` and `FactIdB` FKs to `clinical_facts` with `OnDelete(DeleteBehavior.Cascade)`.
   - Add unique constraint: `HasIndex(a => new { a.PatientId, a.FactIdA, a.FactIdB }).IsUnique()`.
   - Add `DbSet<ConflictRule> ConflictRules { get; set; }` and `DbSet<ConflictAlert> ConflictAlerts { get; set; }`.
6. **Generate migration**: Run `dotnet ef migrations add AddConflictDetectionTables --project src/Modules/ClinicalIntelligence --startup-project src/Api`.
7. **Ensure Down migration** drops tables in dependency order (`conflict_alerts` first, then `conflict_rules`), drops enum types, and drops indexes cleanly.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── ClinicalIntelligenceDbContext.cs      ← MODIFY (add DbSets, enum config, entity config)
│   │   │   └── Migrations/
│   │   │       ├── YYYYMMDDHHMMSS_AddClinicalDocumentsTable.cs         ← EXISTS (US_040)
│   │   │       ├── YYYYMMDDHHMMSS_AddOcrSupport.cs                     ← EXISTS (US_041)
│   │   │       ├── YYYYMMDDHHMMSS_AddFullTextSearchIndex.cs            ← EXISTS (US_042)
│   │   │       ├── YYYYMMDDHHMMSS_AddDocumentManagementColumns.cs      ← EXISTS (US_043)
│   │   │       ├── YYYYMMDDHHMMSS_AddClinicalFactsTable.cs             ← EXISTS (US_044)
│   │   │       └── YYYYMMDDHHMMSS_AddConflictDetectionTables.cs        ← CREATE
│   │   └── Entities/
│   │       ├── ClinicalDocument.cs   ← EXISTS
│   │       ├── ClinicalFact.cs       ← EXISTS (US_044)
│   │       ├── ConflictRule.cs       ← Created in task_002
│   │       └── ConflictAlert.cs      ← Created in task_002
│   └── [existing modules...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/YYYYMMDDHHMMSS_AddConflictDetectionTables.cs` | Migration: create enums, conflict_rules, conflict_alerts, indexes, unique constraint |
| MODIFY | `Modules/ClinicalIntelligence/Data/ClinicalIntelligenceDbContext.cs` | Register enums, add DbSet<ConflictRule>, DbSet<ConflictAlert>, configure entities, FKs, unique index |

---

## External References

- PostgreSQL partial indexes: https://www.postgresql.org/docs/current/indexes-partial.html
- EF Core unique index configuration: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- Npgsql PostgreSQL enum mapping: https://www.npgsql.org/efcore/mapping/enum.html
- FR-CA-003: Drug-drug and drug-allergy conflict detection with severity classification
- NFR-010: Immutable audit evidence — acknowledgments stored with full audit trail (audit_records table)

---

## Build Commands

```bash
# Generate EF Core migration
dotnet ef migrations add AddConflictDetectionTables \
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

- [ ] Migration applies successfully against PostgreSQL 15.x
- [ ] `conflict_type_enum` exists with values: `drug_drug`, `drug_allergy`
- [ ] `severity_level_enum` exists with values: `low`, `moderate`, `high`, `critical`
- [ ] `conflict_rules` table exists with all columns and correct data types
- [ ] `conflict_alerts` table exists with all columns including `acknowledged`, `acknowledged_by`, `acknowledged_at`
- [ ] FK: `conflict_alerts.patient_id` → `patients.patient_id` (`RESTRICT`)
- [ ] FK: `conflict_alerts.fact_id_a` → `clinical_facts.fact_id` (`CASCADE`)
- [ ] FK: `conflict_alerts.fact_id_b` → `clinical_facts.fact_id` (`CASCADE`, nullable)
- [ ] FK: `conflict_alerts.rule_id` → `conflict_rules.rule_id` (`RESTRICT`)
- [ ] FK: `conflict_alerts.acknowledged_by` → `users.user_id` (`SET NULL`)
- [ ] Unique constraint `uq_conflict_alerts_pair` on `(patient_id, fact_id_a, fact_id_b)` prevents duplicate alerts
- [ ] `ix_conflict_rules_type_drugs` partial index on active rules for fast lookup
- [ ] `ix_conflict_alerts_patient_id` B-tree index exists
- [ ] `ix_conflict_alerts_unacknowledged` partial index on `acknowledged = false`
- [ ] Down migration drops both tables and enum types cleanly
- [ ] EF Core model snapshot is consistent with migration

---

## Implementation Checklist

- [ ] Create `conflict_type_enum` PostgreSQL enum (`drug_drug`, `drug_allergy`)
- [ ] Create `severity_level_enum` PostgreSQL enum (`low`, `moderate`, `high`, `critical`)
- [ ] Create `conflict_rules` table with all columns, PK, and `last_updated_at` for staleness detection
- [ ] Create `conflict_alerts` table with all columns, PK, FKs, and `uq_conflict_alerts_pair` unique constraint
- [ ] Create partial indexes on `conflict_rules` (active rules by type+drugs) and `conflict_alerts` (patient, unacknowledged)
- [ ] Configure EF Core entity mappings, enum registrations, FKs, and unique index in `ClinicalIntelligenceDbContext.OnModelCreating()`

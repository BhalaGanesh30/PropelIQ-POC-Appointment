---
task_id: task_003
user_story: us_049
epic: EP-008
layer: Database
status: completed
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_049] ICD-10 Code Suggestion Generation
- **Story Location**: [.propel/context/tasks/EP-008/us_049/us_049.md](.propel/context/tasks/EP-008/us_049/us_049.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient's clinical profile is available, When I request ICD-10 suggestions, Then the system stores each suggestion as a pending coding decision with confidence scores and rationale linked to the source clinical fact.
  - AC-4: Given the suggestion API is called, When the output schema is validated, Then at least 99% of responses pass schema validation with all required fields present — the `coding_decisions` table stores the validated suggestion output, enabling downstream accept/modify/reject workflow.
- **Edge Cases**:
  - Edge Case 1: Fewer than 3 codes generated — insert only the available rows; table accepts 1..3 rows per patient session without constraint violation.
  - Edge Case 2: No clinical facts — HTTP 422 returned before DB insert is attempted; no rows inserted.

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
| Database | PostgreSQL | 15.x |
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

Create the `coding_decisions` table and supporting `reviewer_action_enum` PostgreSQL enum type via an EF Core migration. This table persists AI-generated coding suggestions in a `pending` state until a clinician takes an accept/modify/reject action (US_050+). It is required by `task_002`'s `CodingDecisionRepository.InsertPendingAsync`.

The schema is derived from the `CODING_DECISION` entity in `models.md`: `decision_id`, `patient_id` (FK to `patients`), `fact_id` (FK to `clinical_facts` — primary supporting evidence), `icd10_code`, `cpt_code` (nullable — not used in US_049, reserved for US_050), `confidence` (`DECIMAL(5,4)`), `rationale` (`TEXT`), `reviewer_action` (`reviewer_action_enum`), `reviewer_id` (FK to `users`, nullable until decided), `decided_at` (`TIMESTAMPTZ`, nullable until decided), `created_at` (`TIMESTAMPTZ NOT NULL DEFAULT now()`).

Add indexes for the expected query patterns: patient-based lookup (profile view), fact-based lookup (evidence linking), and pending-action queries.

---

## Dependent Tasks

- **us_044/task_002** — `clinical_facts` table (with `fact_id` PK) must exist for the FK constraint on `coding_decisions.fact_id`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `reviewer_action_enum` | CREATE | PostgreSQL enum: `pending`, `accepted`, `modified`, `rejected` |
| `coding_decisions` table | CREATE | New table with PKs, FKs, enums, confidence decimal, nullable reviewer fields |
| `ix_coding_decisions_patient_id` | CREATE | B-tree index on `patient_id` for patient profile lookups |
| `ix_coding_decisions_fact_id` | CREATE | B-tree index on `fact_id` for evidence-trace queries |
| `ix_coding_decisions_pending` | CREATE | Partial index on `reviewer_action = 'pending'` for pending-queue queries (US_050) |
| EF Core `CodingDecisionEntity` | CREATE | Maps `coding_decisions`; `ReviewerActionEnum` C# enum; configure `created_at` default |
| EF Core migration | CREATE | Additive migration: enum type + table + indexes; no destructive changes |

---

## Implementation Plan

1. **Create `reviewer_action_enum`**: PostgreSQL enum type with values `pending`, `accepted`, `modified`, `rejected`. Map to C# enum `ReviewerAction { Pending, Accepted, Modified, Rejected }` in EF Core using `HasPostgresEnum`.
2. **Create `coding_decisions` table**: Columns: `decision_id UUID PRIMARY KEY DEFAULT gen_random_uuid()`, `patient_id UUID NOT NULL REFERENCES patients(patient_id) ON DELETE CASCADE`, `fact_id UUID NOT NULL REFERENCES clinical_facts(fact_id) ON DELETE RESTRICT`, `icd10_code VARCHAR(20) NOT NULL`, `cpt_code VARCHAR(20) NULL`, `confidence DECIMAL(5,4) NOT NULL CHECK (confidence >= 0 AND confidence <= 1)`, `rationale TEXT NOT NULL`, `reviewer_action reviewer_action_enum NOT NULL DEFAULT 'pending'`, `reviewer_id UUID NULL REFERENCES users(user_id) ON DELETE SET NULL`, `decided_at TIMESTAMPTZ NULL`, `created_at TIMESTAMPTZ NOT NULL DEFAULT now()`.
3. **Add indexes**: `CREATE INDEX ix_coding_decisions_patient_id ON coding_decisions(patient_id)`. `CREATE INDEX ix_coding_decisions_fact_id ON coding_decisions(fact_id)`. `CREATE INDEX ix_coding_decisions_pending ON coding_decisions(patient_id) WHERE reviewer_action = 'pending'` (partial index — accelerates US_050 pending queue).
4. **Create EF Core `CodingDecisionEntity`**: Properties: `Guid DecisionId`, `Guid PatientId`, `Guid FactId`, `string Icd10Code`, `string? CptCode`, `decimal Confidence`, `string Rationale`, `ReviewerAction ReviewerAction`, `Guid? ReviewerId`, `DateTimeOffset? DecidedAt`, `DateTimeOffset CreatedAt`. Configure `ReviewerAction` default via `HasDefaultValue(ReviewerAction.Pending)`.
5. **Add EF Core migration**: Generate migration `AddCodingDecisions` applying the enum type creation and table creation. Verify migration is additive only — no existing table modifications.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── Entities/
│   │   │   │   ├── ClinicalFactEntity.cs       ← EXISTS (US_044)
│   │   │   │   ├── ConflictAlertEntity.cs      ← EXISTS (US_046)
│   │   │   │   └── CodingDecisionEntity.cs     ← CREATE
│   │   │   ├── Migrations/
│   │   │   │   ├── [existing migrations...]
│   │   │   │   └── AddCodingDecisions.cs       ← CREATE (EF Core migration)
│   │   │   └── [existing DbContext...]
│   └── [existing modules...]
Database:
├── clinical_facts                              ← EXISTS (US_044)
├── conflict_rules                              ← EXISTS (US_046)
├── conflict_alerts                             ← EXISTS (US_046)
└── coding_decisions                            ← CREATE (this task)
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Entities/CodingDecisionEntity.cs` | EF Core entity mapping `coding_decisions`; ReviewerAction enum; nullable reviewer fields |
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/AddCodingDecisions.cs` | EF Core migration: reviewer_action_enum + coding_decisions table + 3 indexes |

---

## External References

- EF Core PostgreSQL enums (Npgsql): https://www.npgsql.org/efcore/mapping/enum.html
- EF Core value generated: https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties
- FR-MC-001: Top-3 ICD-10 with confidence and rationale — coding_decisions stores AI output
- FR-MC-003: User decision required (accept/modify/reject) — reviewer_action enum supports this lifecycle
- AIR-004: Citation references — fact_id FK links each decision to its primary supporting fact
- models.md `CODING_DECISION` entity: source of truth for column definitions

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddCodingDecisions --project src/Modules/ClinicalIntelligence

# Apply migration to database
dotnet ef database update --project src/Modules/ClinicalIntelligence

# Verify migration applied
dotnet ef migrations list --project src/Modules/ClinicalIntelligence
```

---

## Implementation Validation Strategy

- [ ] `coding_decisions` table created with all columns matching `CODING_DECISION` entity in `models.md`
- [ ] `reviewer_action_enum` contains exactly: `pending`, `accepted`, `modified`, `rejected`
- [ ] `confidence` column has CHECK constraint (0 ≤ confidence ≤ 1); `DECIMAL(5,4)` precision
- [ ] `fact_id` FK references `clinical_facts(fact_id)` with `ON DELETE RESTRICT` (prevents orphaned evidence links)
- [ ] `patient_id` FK references `patients(patient_id)` with `ON DELETE CASCADE`
- [ ] `reviewer_id` FK has `ON DELETE SET NULL` (user deletion does not corrupt decision history)
- [ ] Partial index `ix_coding_decisions_pending` exists on `reviewer_action = 'pending'` (validates via `\d coding_decisions` in psql)
- [ ] Migration is additive — no modifications to existing tables; `dotnet ef database update` succeeds without errors

---

## Implementation Checklist

- [X] Create `ReviewerAction` C# enum with `Pending`, `Accepted`, `Modified`, `Rejected` values; mapped via `HasConversion<string>()` (column already existed as VARCHAR — no destructive ALTER COLUMN required)
- [X] Update `CodingDecision` entity: enum `ReviewerAction`, `CptCode`, `DecidedAt` properties; `CreatedAt` default (`now()`), `confidence` precision `(5,4)` already configured
- [X] Create EF Core migration `AddCodingDecisionsPendingExtensions`: adds `cpt_code`, `decided_at`, FK `reviewed_by_user_id→users ON DELETE SET NULL`, and `ix_coding_decisions_pending` partial index via `migrationBuilder.Sql()`
- [X] Verify FK constraints: `fact_id → clinical_facts ON DELETE RESTRICT` (exists), `patient_id → patients ON DELETE RESTRICT` (existing constraint preserved), `reviewer_id → users ON DELETE SET NULL` (added in migration)

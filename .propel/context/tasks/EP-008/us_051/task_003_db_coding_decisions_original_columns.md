---
task_id: task_003
user_story: us_051
epic: EP-008
layer: Database
status: completed
effort_hours: 1
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_051] Accept, Modify, and Reject Coding Workflow
- **Story Location**: [.propel/context/tasks/EP-008/us_051/us_051.md](.propel/context/tasks/EP-008/us_051/us_051.md)
- **Acceptance Criteria**:
  - AC-2: Given I want to modify a suggestion, When I click "Modify" and update the code or description, Then the modified code is saved with the original and final values in the audit record — the original value is persisted in `original_icd10_code` / `original_cpt_code` on the `coding_decisions` row for agreement rate tracking.
- **Edge Cases**:
  - Edge Case 2: Agreement rate tracking (AIR-007) — the monitoring dashboard derives the agreement rate by querying `coding_decisions`; storing `original_icd10_code`/`original_cpt_code` enables direct comparison of AI suggestion vs. final clinician value without joining the audit log.

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

Additive migration on the existing `coding_decisions` table (US_049/task_003): add two nullable columns `original_icd10_code` and `original_cpt_code` that store the AI-generated suggestion value before a clinician's Modify action overwrites `icd10_code`/`cpt_code`. These columns are `NULL` for `accepted` and `rejected` rows; populated only when `reviewer_action = modified`.

This enables the AIR-007 monitoring dashboard to compute the agreement rate and diff AI suggestions vs. final decisions directly from `coding_decisions` without parsing the audit log JSONB.

---

## Dependent Tasks

- **us_049/task_003** — `coding_decisions` table must exist.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `coding_decisions.original_icd10_code` | ADD COLUMN | `VARCHAR(20) NULL` — snapshot of AI-suggested ICD-10 code before Modify |
| `coding_decisions.original_cpt_code` | ADD COLUMN | `VARCHAR(20) NULL` — snapshot of AI-suggested CPT code before Modify |
| EF Core `CodingDecisionEntity` | MODIFY | Add `string? OriginalIcd10Code` and `string? OriginalCptCode` nullable properties |
| EF Core migration | CREATE | Additive migration: `AddOriginalCodeColumnsToCodingDecisions` — two `ALTER TABLE ADD COLUMN` statements |

---

## Implementation Plan

1. **Add columns to `coding_decisions`**: `ALTER TABLE coding_decisions ADD COLUMN original_icd10_code VARCHAR(20) NULL`. `ALTER TABLE coding_decisions ADD COLUMN original_cpt_code VARCHAR(20) NULL`. Both nullable — no default value required; existing rows remain `NULL` without disruption.
2. **Update EF Core `CodingDecisionEntity`**: Add `public string? OriginalIcd10Code { get; set; }` and `public string? OriginalCptCode { get; set; }` properties. No additional EF Core configuration required (EF Core maps nullable string properties as nullable columns by convention).
3. **Generate EF Core migration** `AddOriginalCodeColumnsToCodingDecisions`: two `AddColumn` operations. Verify migration contains no `DropColumn` or table modifications.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── Entities/
│   │   │   │   └── CodingDecisionEntity.cs           ← MODIFY (add OriginalIcd10Code, OriginalCptCode)
│   │   │   ├── Migrations/
│   │   │   │   ├── AddCodingDecisions.cs             ← EXISTS (US_049)
│   │   │   │   └── AddOriginalCodeColumnsToCodingDecisions.cs ← CREATE
│   │   │   └── [existing DbContext...]
│   └── [existing modules...]
Database:
└── coding_decisions                                  ← MODIFY (add 2 nullable columns)
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `Modules/ClinicalIntelligence/Data/Entities/CodingDecisionEntity.cs` | Add `string? OriginalIcd10Code` and `string? OriginalCptCode` nullable properties |
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/AddOriginalCodeColumnsToCodingDecisions.cs` | Two AddColumn (VARCHAR 20 NULL); additive only; no existing column changes |

---

## External References

- EF Core AddColumn migration: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- AIR-007: Agreement rate ≥ 98% — original_icd10_code/original_cpt_code enable direct AI-vs-final comparison
- FR-MC-003 [HYBRID]: Human decision required with audit — original values stored for traceability
- NFR-010: Immutable audit evidence — original code snapshot is part of the immutable decision record

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddOriginalCodeColumnsToCodingDecisions --project src/Modules/ClinicalIntelligence

# Apply migration to database
dotnet ef database update --project src/Modules/ClinicalIntelligence

# Verify migration applied
dotnet ef migrations list --project src/Modules/ClinicalIntelligence
```

---

## Implementation Validation Strategy

- [X] `original_icd10_code VARCHAR(20) NULL` column exists on `coding_decisions` (verified via `\d coding_decisions`)
- [X] `original_cpt_code VARCHAR(20) NULL` column exists on `coding_decisions`
- [X] Existing rows in `coding_decisions` retain `NULL` in both new columns after migration (no data disruption)
- [X] Migration contains only `AddColumn` operations — no `DropColumn`, `AlterColumn`, or table renames
- [X] `CodingDecisionEntity` properties `OriginalIcd10Code` and `OriginalCptCode` map correctly as nullable strings in EF Core scaffold

---

## Implementation Checklist

- [X] Add `string? OriginalIcd10Code` and `string? OriginalCptCode` to `CodingDecisionEntity`
- [X] Generate EF Core migration `AddOriginalCodeColumnsToCodingDecisions` with two `AddColumn` (VARCHAR 20 NULL); verify no destructive operations in generated SQL

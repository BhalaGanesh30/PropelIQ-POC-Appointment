---
task_id: task_003
user_story: us_050
epic: EP-008
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_050] CPT and E/M Mapping Suggestions
- **Story Location**: [.propel/context/tasks/EP-008/us_050/us_050.md](.propel/context/tasks/EP-008/us_050/us_050.md)
- **Acceptance Criteria**:
  - AC-1: Given a patient's clinical profile and appointment details are available, When I request CPT/E/M suggestions, Then ranked CPT codes are returned — these codes must be validated against the `cpt_codes` reference table (only active, non-deprecated codes are returned).
  - AC-2: Given CPT suggestions are displayed, When I view a suggestion card, Then the CPT code and description are visible — both sourced from `cpt_codes`.
- **Edge Cases**:
  - Edge Case 1: Unmappable appointment type — no DB interaction required; handled at service layer before table is queried.
  - Edge Case 2: CPT database older than 90 days — `cpt_codes.last_updated_at` is the timestamp queried by `CptCodeFreshnessService`; `is_deprecated = true` rows are excluded from suggestions at query time.

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

Create the `cpt_codes` reference table via an EF Core migration. This table serves as the deterministic guardrail layer for the Hybrid CPT suggestion pipeline in `task_002`: LLM-suggested codes are validated against this table to exclude deprecated or non-existent codes before being returned to the FE.

The table stores the CPT code catalog with deprecation status and freshness tracking: `cpt_code` (PK), `description`, `category`, `is_deprecated`, `effective_date`, `deprecation_date`, and `last_updated_at`. The `last_updated_at` column is queried by `CptCodeFreshnessService` to determine if the catalog is older than 90 days (Edge Case 2).

This is an additive migration with no changes to existing tables.

---

## Dependent Tasks

- No upstream task dependencies — standalone reference table with no FKs to other tables.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `cpt_codes` table | CREATE | CPT reference catalog: code PK, description, category, deprecation status, freshness timestamp |
| `ix_cpt_codes_active` | CREATE | Partial index `WHERE is_deprecated = false` — accelerates active-code queries |
| `ix_cpt_codes_last_updated` | CREATE | B-tree index on `last_updated_at` — accelerates freshness check query |
| EF Core `CptCodeEntity` | CREATE | Maps `cpt_codes`; no FK relationships; `CptCode` is the natural string PK |
| EF Core migration | CREATE | Additive migration: `AddCptCodesReferenceTable` |

---

## Implementation Plan

1. **Create `cpt_codes` table**: Columns: `cpt_code VARCHAR(20) PRIMARY KEY`, `description TEXT NOT NULL`, `category VARCHAR(100) NULL` (e.g., "Surgery", "Medicine", "E/M Services"), `is_deprecated BOOLEAN NOT NULL DEFAULT false`, `effective_date DATE NULL`, `deprecation_date DATE NULL` (set when `is_deprecated` transitions to `true`), `last_updated_at TIMESTAMPTZ NOT NULL DEFAULT now()`. No FK relationships — this is a standalone reference table populated by an external catalog update process.
2. **Add indexes**: `CREATE INDEX ix_cpt_codes_active ON cpt_codes(cpt_code) WHERE is_deprecated = false` (partial index — most queries filter on active codes). `CREATE INDEX ix_cpt_codes_last_updated ON cpt_codes(last_updated_at DESC)` (freshness check uses `MAX(last_updated_at)` scan).
3. **Create EF Core `CptCodeEntity`**: Properties: `string CptCode` (string PK — not Guid), `string Description`, `string? Category`, `bool IsDeprecated`, `DateOnly? EffectiveDate`, `DateOnly? DeprecationDate`, `DateTimeOffset LastUpdatedAt`. Configure `CptCode` as primary key (`HasKey(c => c.CptCode)`); no auto-generated value on PK (natural key from catalog).
4. **Generate EF Core migration** `AddCptCodesReferenceTable`: includes table creation and both indexes. Verify migration is additive only.

---

## Current Project State

```
src/
├── Modules/
│   ├── ClinicalIntelligence/
│   │   ├── Data/
│   │   │   ├── Entities/
│   │   │   │   ├── ClinicalFactEntity.cs           ← EXISTS (US_044)
│   │   │   │   ├── ConflictRuleEntity.cs           ← EXISTS (US_046)
│   │   │   │   ├── CodingDecisionEntity.cs         ← EXISTS (US_049)
│   │   │   │   └── CptCodeEntity.cs                ← CREATE
│   │   │   ├── Migrations/
│   │   │   │   ├── [existing migrations...]
│   │   │   │   └── AddCptCodesReferenceTable.cs    ← CREATE (EF Core migration)
│   │   │   └── [existing DbContext...]
│   └── [existing modules...]
Database:
├── clinical_facts                                  ← EXISTS (US_044)
├── conflict_rules                                  ← EXISTS (US_046)
├── conflict_alerts                                 ← EXISTS (US_046)
├── coding_decisions                                ← EXISTS (US_049)
└── cpt_codes                                       ← CREATE (this task)
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/ClinicalIntelligence/Data/Entities/CptCodeEntity.cs` | EF Core entity mapping `cpt_codes`; string natural PK; deprecation and freshness columns |
| CREATE | `Modules/ClinicalIntelligence/Data/Migrations/AddCptCodesReferenceTable.cs` | Additive migration: cpt_codes table + ix_cpt_codes_active partial index + ix_cpt_codes_last_updated |

---

## External References

- EF Core string primary keys: https://learn.microsoft.com/en-us/ef/core/modeling/keys
- EF Core DateOnly support (Npgsql): https://www.npgsql.org/efcore/mapping/datetime.html
- FR-MC-002 [HYBRID]: CPT and E/M mapping suggestions — cpt_codes is the deterministic guardrail layer
- Edge Case 2: CPT code deprecations — `is_deprecated` flag + `last_updated_at` freshness tracking

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddCptCodesReferenceTable --project src/Modules/ClinicalIntelligence

# Apply migration to database
dotnet ef database update --project src/Modules/ClinicalIntelligence

# Verify migration applied
dotnet ef migrations list --project src/Modules/ClinicalIntelligence
```

---

## Implementation Validation Strategy

- [ ] `cpt_codes` table created with all columns: `cpt_code` (string PK), `description`, `category`, `is_deprecated`, `effective_date`, `deprecation_date`, `last_updated_at`
- [ ] `is_deprecated` defaults to `false`; `last_updated_at` defaults to `now()`
- [ ] Partial index `ix_cpt_codes_active` exists on `cpt_code WHERE is_deprecated = false`
- [ ] Index `ix_cpt_codes_last_updated` exists on `last_updated_at DESC` for freshness check performance
- [ ] No FK constraints on `cpt_codes` — standalone reference table (verified via `\d cpt_codes`)
- [ ] Migration is additive — no modifications to existing tables; `dotnet ef database update` succeeds without errors
- [ ] A row with `is_deprecated = true` is NOT returned by `CptCodeRepository.ExistsAndActiveAsync()`

---

## Implementation Checklist

- [ ] Create `CptCodeEntity` with string natural PK (`cpt_code`), `IsDeprecated` bool, `DeprecationDate DateOnly?`, `EffectiveDate DateOnly?`, `LastUpdatedAt DateTimeOffset`; configure `HasKey(c => c.CptCode)` with no value generation
- [ ] Create EF Core migration `AddCptCodesReferenceTable`: `cpt_codes` table, `ix_cpt_codes_active` partial index (`WHERE is_deprecated = false`), `ix_cpt_codes_last_updated` index on `last_updated_at DESC`
- [ ] Verify no FK constraints; verify `is_deprecated DEFAULT false` and `last_updated_at DEFAULT now()` column defaults present in generated SQL

---
task_id: task_003
user_story: us_034
epic: EP-004
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_034] Scheduling Override with Mandatory Audit
- **Story Location**: [.propel/context/tasks/EP-004/us_034/us_034.md](.propel/context/tasks/EP-004/us_034/us_034.md)
- **Acceptance Criteria**:
  - AC-2: When the override is processed, an audit record is created capturing the staff member's identity, the overridden constraint, the reason, and the timestamp.
  - AC-4: Admins can filter audit records by action type "Override" to list all override events with full reason and actor details.
- **Edge Cases**:
  - Edge Case 1: Override reason capped at 500 characters; `override_reason` column enforces `varchar(500)`.

---

## Design References (Database Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
| Migration | EF Core Migrations | 8.x |
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

Create the database migration to support scheduling override audit records. This migration extends the existing `audit_records` table with dedicated columns for override-specific data: `override_constraint_type` to capture which scheduling rule was bypassed, `override_reason` to store the mandatory staff-provided reason (capped at 500 characters), and `override_action` to record the action taken (cancel, reschedule, force-book). An index is added on `event_type` to accelerate filtered queries for override events per AC-4. All changes are additive-only columns with nullable defaults to maintain backward compatibility with existing audit records per DR-007 (zero-downtime rollout). The `audit_records` table remains append-only per NFR-010 and DR-005.

---

## Dependent Tasks

- None — the `audit_records` table already exists from EP-DATA foundation. This migration adds columns to the existing table.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `audit_records.override_constraint_type` column | CREATE | Nullable `varchar(50)` for constraint type |
| `audit_records.override_reason` column | CREATE | Nullable `varchar(500)` for staff-provided reason |
| `audit_records.override_action` column | CREATE | Nullable `varchar(20)` for override action |
| `IX_audit_records_event_type` index | CREATE | B-tree index on `event_type` for filtered audit queries |
| `IX_audit_records_event_type_created_at` index | CREATE | Composite index for `event_type` + `created_at` for time-range filtered override queries |
| `AuditRecord` EF entity | MODIFY | Add `OverrideConstraintType`, `OverrideReason`, `OverrideAction` properties |
| `AuditRecordConfiguration` | MODIFY | Map new columns with nullable configuration and max-length constraints |
| Migration file | CREATE | EF Core migration with `Up()` and `Down()` |

---

## Implementation Plan

1. **Add columns to `audit_records`** table via EF Core migration:
   - `override_constraint_type` (varchar(50), NULL) — stores enum name: `CancellationWithin24Hours`, `RescheduleWithin24Hours`, `SlotConflict`, `CapacityExceeded`.
   - `override_reason` (varchar(500), NULL) — stores the mandatory reason text. Nullable at the DB level because non-override audit records do not populate this column.
   - `override_action` (varchar(20), NULL) — stores enum name: `Cancel`, `Reschedule`, `ForceBook`.
   - All columns are nullable to preserve backward compatibility with existing audit records (additive-only per DR-007).
2. **Create index `IX_audit_records_event_type`**: B-tree index on `event_type` column to accelerate `WHERE event_type = 'Override'` filter queries for AC-4.
3. **Create composite index `IX_audit_records_event_type_created_at`**: B-tree on `(event_type, created_at DESC)` to support time-range filtered override queries in the admin audit log.
4. **Update `AuditRecord` EF entity** in `SharedServices/Audit/Domain/AuditRecord.cs`: add `string? OverrideConstraintType`, `string? OverrideReason`, `string? OverrideAction` properties.
5. **Update `AuditRecordConfiguration`**: configure new columns with `.HasMaxLength(500)` on `OverrideReason`, `.HasMaxLength(50)` on `OverrideConstraintType`, `.HasMaxLength(20)` on `OverrideAction`, all `.IsRequired(false)`.
6. **Write `Down()` migration**: drop the two indexes, then drop the three columns. The operation is safe because existing audit records do not use these columns.

---

## Current Project State

```
Server/
├── Modules/
│   ├── SharedServices/
│   │   └── Audit/
│   │       ├── Domain/
│   │       │   └── AuditRecord.cs                         ← MODIFY (add override properties)
│   │       └── Data/
│   │           └── AuditRecordConfiguration.cs            ← MODIFY (map new columns)
├── Data/
│   ├── AppDbContext.cs                                     ← EXISTS
│   └── Migrations/
│       └── YYYYMMDDHHMMSS_AddOverrideAuditColumns.cs     ← CREATE
└── [existing structure...]
```

> Placeholder: Update this tree after EP-DATA tasks are complete and the actual audit module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Data/Migrations/YYYYMMDDHHMMSS_AddOverrideAuditColumns.cs` | EF Core migration: add override columns and indexes to `audit_records` |
| MODIFY | `Server/Modules/SharedServices/Audit/Domain/AuditRecord.cs` | Add `OverrideConstraintType`, `OverrideReason`, `OverrideAction` nullable properties |
| MODIFY | `Server/Modules/SharedServices/Audit/Data/AuditRecordConfiguration.cs` | Map new columns with max-length and nullable configuration |

---

## External References

- EF Core 8 migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli
- PostgreSQL B-tree indexes: https://www.postgresql.org/docs/15/indexes-types.html
- EF Core 8 entity type configuration: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types
- NFR-010: Immutable audit evidence with 7-year retention
- DR-005: Append-only audit table with write-restriction policies
- DR-007: Schema migration with backward-compatible, zero-downtime rollouts

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddOverrideAuditColumns --project Server

# Apply migration
dotnet ef database update --project Server

# Verify migration rollback
dotnet ef database update PREVIOUS_MIGRATION --project Server

# Build
dotnet build
```

---

## Implementation Validation Strategy

- [ ] Migration applies successfully on a database with existing audit records (backward-compatible)
- [ ] Existing audit records retain NULL values for new override columns (no data corruption)
- [ ] `Down()` migration rolls back cleanly without affecting non-override audit records
- [ ] `override_reason` column enforces `varchar(500)` max length at the database level
- [ ] `IX_audit_records_event_type` index accelerates `WHERE event_type = 'Override'` queries (verify with `EXPLAIN ANALYZE`)
- [ ] Composite index `IX_audit_records_event_type_created_at` supports time-range filtered queries

---

## Implementation Checklist

- [ ] Add `override_constraint_type` (varchar(50), NULL), `override_reason` (varchar(500), NULL), `override_action` (varchar(20), NULL) columns to `audit_records`
- [ ] Create B-tree index `IX_audit_records_event_type` on `event_type` column
- [ ] Create composite index `IX_audit_records_event_type_created_at` on `(event_type, created_at DESC)`
- [ ] Update `AuditRecord` entity with nullable override properties
- [ ] Update `AuditRecordConfiguration` with column mappings and max-length constraints
- [ ] Write `Down()` migration with complete rollback support

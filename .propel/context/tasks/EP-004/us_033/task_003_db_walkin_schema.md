---
task_id: task_003
user_story: us_033
epic: EP-004
layer: Database
status: not-started
effort_hours: 3
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_033] Walk-In Creation and Patient Registration Conversion
- **Story Location**: [.propel/context/tasks/EP-004/us_033/us_033.md](.propel/context/tasks/EP-004/us_033/us_033.md)
- **Acceptance Criteria**:
  - AC-1: Staff creates a walk-in entry with patient name and visit reason; a temporary walk-in record is created and inserted into the queue with an estimated wait-time position.
  - AC-2: Staff initiates patient registration for a walk-in; a new patient account is created and the walk-in record is associated with the new patient profile.
  - AC-4: Existing patient search by name or phone number finds the profile; walk-in created against existing account without duplication.
- **Edge Cases**:
  - Edge Case 1: Multiple patients match search; patient search requires performant index on name and phone columns.

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

Create the database migration to support walk-in patient tracking. This includes a new `walk_ins` table for temporary walk-in records, an `appointment_type` column on the `appointments` table to distinguish walk-in entries from scheduled appointments, and a trigram-based GIN index on the `patients` table for performant name and phone search. The migration follows DR-001 (globally unique identifiers and explicit FK relationships), DR-002 (referential integrity for queue insertion), and DR-007 (backward-compatible, zero-downtime rollout). All changes are additive-only to support zero-downtime deployment.

---

## Dependent Tasks

- **us_031/task_004** — Queue state migration must be applied first so `queue_state`, `arrived_at`, `visit_started_at`, and `visit_ended_at` columns exist on the `appointments` table.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `walk_ins` table | CREATE | New table for temporary walk-in records |
| `WalkIn` EF entity configuration | CREATE | EF Core `IEntityTypeConfiguration<WalkIn>` with FK constraints |
| `appointments.appointment_type` column | CREATE | New column with default `Scheduled` for backward compatibility |
| `patients` trigram index | CREATE | GIN index using `pg_trgm` for fast name/phone search |
| `AppDbContext` | MODIFY | Register `WalkIn` DbSet and entity configuration |
| Migration file | CREATE | EF Core migration with `Up()` and `Down()` methods |

---

## Implementation Plan

1. **Enable `pg_trgm` extension**: Add `CREATE EXTENSION IF NOT EXISTS pg_trgm;` in migration `Up()` to enable trigram-based similarity search. This is a no-op if already enabled.
2. **Create `walk_ins` table** via EF Core migration:
   - `walk_in_id` (uuid, PK, default `gen_random_uuid()`)
   - `patient_name` (varchar(200), NOT NULL)
   - `phone` (varchar(20), NULL)
   - `visit_reason` (varchar(500), NOT NULL)
   - `patient_id` (uuid, NULL, FK → `patients.patient_id` ON DELETE SET NULL)
   - `appointment_id` (uuid, NULL, FK → `appointments.appointment_id` ON DELETE SET NULL)
   - `is_converted` (boolean, NOT NULL, default `false`)
   - `created_at` (timestamptz, NOT NULL, default `now()`)
   - `created_by` (uuid, NOT NULL, FK → `users.user_id`)
   - Add index on `patient_id` for FK lookup.
   - Add index on `created_at` for date-filtered queries.
3. **Add `appointment_type` column** to `appointments` table:
   - `appointment_type` (varchar(20), NOT NULL, default `'Scheduled'`)
   - This is additive-only; existing rows default to `Scheduled`, satisfying DR-007 zero-downtime rollout.
   - Add index on `appointment_type` for filtered queue queries.
4. **Create trigram GIN indexes** on `patients` table for search performance:
   - `IX_patients_first_name_trgm`: GIN index on `first_name` using `gin_trgm_ops`.
   - `IX_patients_last_name_trgm`: GIN index on `last_name` using `gin_trgm_ops`.
   - `IX_patients_phone_trgm`: GIN index on `phone` using `gin_trgm_ops`.
5. **Create EF Core entity configuration** for `WalkIn` in `Scheduling/Data/WalkInConfiguration.cs`: configure table name, FK relationships, column types, and default values.
6. **Write `Down()` migration**: drop trigram indexes, drop `appointment_type` column, drop `walk_ins` table, drop `pg_trgm` extension (only if no other dependents).

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Data/
│   │   │   ├── AppointmentConfiguration.cs     ← EXISTS
│   │   │   └── WalkInConfiguration.cs          ← CREATE
│   │   └── Domain/
│   │       └── Entities/
│   │           ├── Appointment.cs              ← MODIFY (add AppointmentType property)
│   │           └── WalkIn.cs                   ← CREATE (task_002 creates domain entity)
├── Data/
│   ├── AppDbContext.cs                          ← MODIFY (add WalkIn DbSet)
│   └── Migrations/
│       └── YYYYMMDDHHMMSS_AddWalkInSupport.cs  ← CREATE
└── [existing structure...]
```

> Placeholder: Update this tree after us_031/task_004 is complete and the actual migration folder is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Data/Migrations/YYYYMMDDHHMMSS_AddWalkInSupport.cs` | EF Core migration: `walk_ins` table, `appointment_type` column, trigram indexes |
| CREATE | `Server/Modules/Scheduling/Data/WalkInConfiguration.cs` | EF Core `IEntityTypeConfiguration<WalkIn>` with FK constraints and column mappings |
| MODIFY | `Server/Modules/Scheduling/Domain/Entities/Appointment.cs` | Add `AppointmentType` property (string, default `"Scheduled"`) |
| MODIFY | `Server/Data/AppDbContext.cs` | Register `DbSet<WalkIn>` and apply `WalkInConfiguration` |

---

## External References

- EF Core 8 migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli
- PostgreSQL `pg_trgm` extension for trigram search: https://www.postgresql.org/docs/15/pgtrgm.html
- Npgsql EF Core provider `ILike` and trigram support: https://www.npgsql.org/efcore/mapping/full-text-search.html
- EF Core 8 entity type configuration: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types
- DR-001: Globally unique identifiers and explicit FK relationships
- DR-002: Referential integrity and transactional consistency
- DR-007: Schema migration with backward-compatible, zero-downtime rollouts

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddWalkInSupport --project Server

# Apply migration
dotnet ef database update --project Server

# Verify migration rollback
dotnet ef database update PREVIOUS_MIGRATION --project Server

# Build
dotnet build
```

---

## Implementation Validation Strategy

- [ ] Migration applies successfully on a clean database
- [ ] Migration applies successfully on a database with existing appointment rows (backward-compatible)
- [ ] `Down()` migration rolls back cleanly without data loss in unrelated tables
- [ ] `walk_ins` table FK constraints enforced (invalid `patient_id` rejected)
- [ ] `appointment_type` column defaults to `Scheduled` for existing rows
- [ ] Trigram index on `patients.first_name`, `patients.last_name`, and `patients.phone` accelerates `ILIKE` queries (verify with `EXPLAIN ANALYZE`)

---

## Implementation Checklist

- [ ] Add `CREATE EXTENSION IF NOT EXISTS pg_trgm` in migration `Up()`
- [ ] Create `walk_ins` table with all columns, FK constraints, and indexes
- [ ] Add `appointment_type` column to `appointments` table with default `'Scheduled'`
- [ ] Create GIN trigram indexes on `patients.first_name`, `patients.last_name`, and `patients.phone`
- [ ] Create `WalkInConfiguration.cs` with EF Core entity mapping
- [ ] Write `Down()` migration with complete rollback support

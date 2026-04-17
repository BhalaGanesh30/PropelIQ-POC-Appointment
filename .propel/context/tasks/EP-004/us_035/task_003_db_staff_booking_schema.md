---
task_id: task_003
user_story: us_035
epic: EP-004
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_035] Staff-Assisted Patient Booking
- **Story Location**: [.propel/context/tasks/EP-004/us_035/us_035.md](.propel/context/tasks/EP-004/us_035/us_035.md)
- **Acceptance Criteria**:
  - AC-2: Booking is attributed to the staff member who created it.
  - AC-4: Audit log shows the booking was created by a staff actor on behalf of the patient.
- **Edge Cases**:
  - Edge Case 1: Conflicting appointment check requires an index on `patient_id + date_time` for performant overlap detection.

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

Create the database migration to support staff-assisted booking attribution and efficient conflict detection. This migration adds a `created_by_staff_id` column to the `appointments` table as a nullable FK to `users`, enabling audit attribution when a staff member creates a booking on behalf of a patient. A composite index on `(patient_id, date_time)` is added to the `appointments` table to accelerate conflict-check queries that detect overlapping appointments for a given patient. All changes are additive-only columns with nullable defaults to maintain backward compatibility per DR-007 (zero-downtime rollout). Existing patient self-bookings retain a NULL value for `created_by_staff_id`.

---

## Dependent Tasks

- **us_031/task_004** — Queue state migration must be applied first so the `appointments` table has the baseline queue columns.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `appointments.created_by_staff_id` column | CREATE | Nullable uuid FK to `users.user_id` |
| `IX_appointments_patient_datetime` index | CREATE | Composite B-tree on `(patient_id, date_time)` for conflict detection |
| `IX_appointments_created_by_staff` index | CREATE | B-tree on `created_by_staff_id` for staff booking attribution queries |
| `Appointment` EF entity | MODIFY | Add `CreatedByStaffId` property and navigation to `User` |
| `AppointmentConfiguration` | MODIFY | Map `created_by_staff_id` FK with `ON DELETE SET NULL` |
| Migration file | CREATE | EF Core migration with `Up()` and `Down()` |

---

## Implementation Plan

1. **Add `created_by_staff_id` column** to `appointments` table via EF Core migration:
   - `created_by_staff_id` (uuid, NULL, FK → `users.user_id` ON DELETE SET NULL)
   - Nullable because patient self-bookings do not have a staff actor. Existing rows default to NULL.
   - Additive-only per DR-007 for zero-downtime rollout.
2. **Create composite index `IX_appointments_patient_datetime`**: B-tree on `(patient_id, date_time)` to accelerate conflict-check queries (`WHERE patient_id = @patientId AND date_time BETWEEN @start AND @end`).
3. **Create index `IX_appointments_created_by_staff`**: B-tree on `created_by_staff_id` to support audit queries filtering by staff actor.
4. **Update `Appointment` EF entity**: Add `Guid? CreatedByStaffId` property with `[ForeignKey]` to `User`. Add `User? CreatedByStaff` navigation property.
5. **Update `AppointmentConfiguration`**: Configure FK with `.HasOne(a => a.CreatedByStaff).WithMany().HasForeignKey(a => a.CreatedByStaffId).OnDelete(DeleteBehavior.SetNull)`. Configure column as `.IsRequired(false)`.
6. **Write `Down()` migration**: Drop the indexes, then drop the `created_by_staff_id` column. Safe because existing appointment data does not depend on this column.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Data/
│   │   │   └── AppointmentConfiguration.cs        ← MODIFY (add CreatedByStaffId FK)
│   │   └── Domain/
│   │       └── Entities/
│   │           └── Appointment.cs                  ← MODIFY (add CreatedByStaffId property)
├── Data/
│   ├── AppDbContext.cs                              ← EXISTS
│   └── Migrations/
│       └── YYYYMMDDHHMMSS_AddStaffBookingAttribution.cs  ← CREATE
└── [existing structure...]
```

> Placeholder: Update this tree after us_031/task_004 is complete and the actual migration folder is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Data/Migrations/YYYYMMDDHHMMSS_AddStaffBookingAttribution.cs` | EF Core migration: `created_by_staff_id` column and indexes |
| MODIFY | `Server/Modules/Scheduling/Domain/Entities/Appointment.cs` | Add `CreatedByStaffId` nullable property and `CreatedByStaff` navigation |
| MODIFY | `Server/Modules/Scheduling/Data/AppointmentConfiguration.cs` | Map FK with ON DELETE SET NULL and column configuration |

---

## External References

- EF Core 8 migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli
- EF Core 8 relationships and FK configuration: https://learn.microsoft.com/en-us/ef/core/modeling/relationships
- PostgreSQL composite indexes: https://www.postgresql.org/docs/15/indexes-multicolumn.html
- DR-001: Globally unique identifiers and explicit FK relationships
- DR-002: Referential integrity and transactional consistency
- DR-007: Schema migration with backward-compatible, zero-downtime rollouts

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddStaffBookingAttribution --project Server

# Apply migration
dotnet ef database update --project Server

# Verify migration rollback
dotnet ef database update PREVIOUS_MIGRATION --project Server

# Build
dotnet build
```

---

## Implementation Validation Strategy

- [ ] Migration applies successfully on a database with existing appointment rows (backward-compatible)
- [ ] Existing appointment rows retain NULL for `created_by_staff_id` (no data corruption)
- [ ] `Down()` migration rolls back cleanly without affecting existing appointment data
- [ ] FK constraint enforced: invalid `created_by_staff_id` rejected
- [ ] `IX_appointments_patient_datetime` index accelerates conflict queries (verify with `EXPLAIN ANALYZE`)
- [ ] `IX_appointments_created_by_staff` index supports staff attribution queries

---

## Implementation Checklist

- [ ] Add `created_by_staff_id` (uuid, NULL, FK → `users.user_id` ON DELETE SET NULL) to `appointments` table
- [ ] Create composite index `IX_appointments_patient_datetime` on `(patient_id, date_time)`
- [ ] Create index `IX_appointments_created_by_staff` on `created_by_staff_id`
- [ ] Update `Appointment` entity with `CreatedByStaffId` property and navigation
- [ ] Update `AppointmentConfiguration` with FK mapping and ON DELETE SET NULL behavior
- [ ] Write `Down()` migration with complete rollback support

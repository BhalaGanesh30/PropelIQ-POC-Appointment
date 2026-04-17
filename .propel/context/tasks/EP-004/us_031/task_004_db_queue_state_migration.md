---
task_id: task_004
user_story: us_031
epic: EP-004
layer: Database
status: not-started
effort_hours: 3
---

# Task - task_004

## Requirement Reference

- **User Story**: [us_031] Real-Time Queue Dashboard
- **Story Location**: [.propel/context/tasks/EP-004/us_031/us_031.md](.propel/context/tasks/EP-004/us_031/us_031.md)
- **Acceptance Criteria**:
  - AC-1: Queue dashboard displays status badges — requires `QueueState` column on `Appointments` table.
  - AC-2: Status updates refresh the entry — requires `QueueState` to be a mutable column updated by check-in transitions.
  - AC-3: Overdue detection requires `ArrivedAt` timestamp.
- **Edge Cases**:
  - Edge Case 2: 100+ patient query must remain fast — requires a composite index on `(AppointmentDate, QueueState)`.

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
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
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

Apply an EF Core 8 migration to the `Appointments` table to add the queue-state tracking columns required by US_031. Add `QueueState` (integer enum column, default `0 = Scheduled`), `ArrivedAt` (TIMESTAMPTZ nullable), `VisitStartedAt` (TIMESTAMPTZ nullable), and `VisitEndedAt` (TIMESTAMPTZ nullable). Create a composite index on `(AppointmentDate, QueueState)` for efficient today's-queue queries. Update the EF Core `Appointment` entity class and `AppointmentDbContext` configuration to include the new columns. The migration must include a rollback (Down) method.

> **Dependency note**: US_031 declares a foundational dependency on US_009 (Appointment entity). If US_009 has already added some of these fields, audit the existing migration before applying new columns to avoid duplication.

---

## Dependent Tasks

- None — this task has no hard dependency on other tasks in this user story. It is the lowest-layer task and must complete first to unblock task_002 and task_001 end-to-end testing.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `Appointment` entity | MODIFY | Add `QueueState`, `ArrivedAt`, `VisitStartedAt`, `VisitEndedAt` properties |
| `AppointmentDbContext` (or `SchedulingDbContext`) | MODIFY | Add Fluent API column configuration for new fields |
| EF Core Migration | CREATE | `AddQueueStateFieldsToAppointments` migration with Up/Down |
| `idx_appointments_queue_date` index | CREATE | Composite index on `(AppointmentDate, QueueState)` |

---

## Implementation Plan

1. **Audit existing `Appointments` schema**: Check the most recent EF Core migration for any existing `QueueState`, `ArrivedAt`, `VisitStartedAt`, `VisitEndedAt` columns (added by US_009 tasks). If already present, skip those columns and only add missing ones.
2. **Update `Appointment` entity** in `Scheduling/Domain/Entities/Appointment.cs`:
   - Add `public QueueState QueueState { get; set; } = QueueState.Scheduled;`
   - Add `public DateTimeOffset? ArrivedAt { get; set; }`
   - Add `public DateTimeOffset? VisitStartedAt { get; set; }`
   - Add `public DateTimeOffset? VisitEndedAt { get; set; }`
3. **Update `AppointmentDbContext` Fluent API** (or `AppointmentEntityTypeConfiguration`):
   - `.Property(a => a.QueueState).HasColumnType("integer").HasDefaultValue(QueueState.Scheduled).IsRequired()`
   - `.Property(a => a.ArrivedAt).HasColumnType("timestamptz")`
   - `.Property(a => a.VisitStartedAt).HasColumnType("timestamptz")`
   - `.Property(a => a.VisitEndedAt).HasColumnType("timestamptz")`
   - `.HasIndex(a => new { a.AppointmentDate, a.QueueState }).HasDatabaseName("idx_appointments_queue_date")`
4. **Generate EF Core migration**: Run `dotnet ef migrations add AddQueueStateFieldsToAppointments --project Server` to scaffold the Up/Down SQL.
5. **Verify migration SQL**: Open the generated `.cs` migration file, confirm `migrationBuilder.AddColumn` statements for each column, `CreateIndex` for the composite index, and a valid `Down()` that drops the index then drops the columns in reverse order.
6. **Apply migration locally**: Run `dotnet ef database update` against the local PostgreSQL dev database; verify via `psql \d appointments`.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Scheduling/
│   │   ├── Domain/
│   │   │   └── Entities/
│   │   │       └── Appointment.cs    ← MODIFY (add 4 new properties)
│   │   └── Data/
│   │       └── AppointmentDbContext.cs (or SchedulingDbContext.cs)  ← MODIFY
└── Migrations/
    └── [existing migrations...]
    └── [timestamp]_AddQueueStateFieldsToAppointments.cs  ← CREATE (generated)
```

> Placeholder: Update tree once the actual EF Core migration folder location and `DbContext` class name are confirmed during execution.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `Server/Modules/Scheduling/Domain/Entities/Appointment.cs` | Add `QueueState`, `ArrivedAt`, `VisitStartedAt`, `VisitEndedAt` properties |
| MODIFY | `Server/Modules/Scheduling/Data/SchedulingDbContext.cs` | Add Fluent API config for 4 new columns + composite index |
| CREATE | `Server/Migrations/[timestamp]_AddQueueStateFieldsToAppointments.cs` | EF Core migration — Up: AddColumn ×4 + CreateIndex; Down: DropIndex + DropColumn ×4 |

---

## External References

- EF Core 8 migrations with PostgreSQL (Npgsql): https://www.npgsql.org/efcore/index.html
- EF Core 8 `HasIndex` Fluent API: https://learn.microsoft.com/en-us/ef/core/modeling/indexes?tabs=data-annotations
- EF Core 8 `HasDefaultValue` for enum columns: https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties?tabs=data-annotations
- PostgreSQL `TIMESTAMPTZ` column type with Npgsql EF Core: https://www.npgsql.org/efcore/mapping/datetime.html
- EF Core migration `Up` / `Down` reference: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli

---

## Build Commands

```bash
# Add migration (run from solution root or Server project)
dotnet ef migrations add AddQueueStateFieldsToAppointments --project Server

# Apply migration to local dev DB
dotnet ef database update --project Server

# Rollback migration (if needed)
dotnet ef database update [previous_migration_name] --project Server

# Verify applied migrations
dotnet ef migrations list --project Server
```

---

## Implementation Validation Strategy

- [ ] Migration `Up()` generates valid SQL without errors against local PostgreSQL 15 database
- [ ] `\d appointments` in psql confirms 4 new columns: `queue_state`, `arrived_at`, `visit_started_at`, `visit_ended_at`
- [ ] `\di appointments_*` in psql confirms `idx_appointments_queue_date` composite index exists
- [ ] Migration `Down()` successfully drops the index and all 4 columns (rollback verified locally)
- [ ] `Appointment` entity in C# compiles without errors after adding new properties
- [ ] `QueueService` (task_002) can query `QueueState` and `ArrivedAt` fields without EF Core mapping errors
- [ ] Existing `Appointments` rows retain their data after migration (non-destructive column additions)

---

## Implementation Checklist

- [ ] Audit existing migrations to check for any pre-existing `QueueState` / `ArrivedAt` columns from US_009 tasks — only add truly new columns
- [ ] Add `QueueState`, `ArrivedAt`, `VisitStartedAt`, `VisitEndedAt` properties to `Appointment` entity
- [ ] Add Fluent API column configuration in `SchedulingDbContext` (column types, default value, nullable)
- [ ] Add `HasIndex(a => new { a.AppointmentDate, a.QueueState }).HasDatabaseName("idx_appointments_queue_date")` to Fluent config
- [ ] Run `dotnet ef migrations add AddQueueStateFieldsToAppointments` and review generated SQL
- [ ] Run `dotnet ef database update` against local dev database and verify schema with `\d appointments`
- [ ] Verify migration `Down()` is valid by running rollback locally, then re-applying

# Task - TASK_002

## Requirement Reference

- User Story: us_061
- Story Location: .propel/context/tasks/EP-011/us_061/us_061.md
- Acceptance Criteria:
  - AC-2: Given I select multiple users using checkboxes, When I apply a bulk action (Activate, Deactivate, or Assign Role), Then the action is applied to all selected users in a single operation and each change is recorded in the audit log.
  - AC-3: Given I view a specific user's profile, When I open their activity history, Then recent login events, role changes, and actions performed are listed in reverse chronological order.
- Edge Cases:
  - What happens if a bulk action would deactivate all admin accounts? System validates the action and blocks it with: "Cannot deactivate all admin accounts. At least one admin must remain active."

## Design References (Frontend Tasks Only)

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

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | N/A | N/A |
| Database | PostgreSQL | 15.x |
| Library | EF Core with Npgsql | 8.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Create the PostgreSQL schema supporting user activity history for the user management feature. The `user_activity_log` table stores login events, role changes, status changes, and actions performed by or on a user. Each row captures the event type, a human-readable description, the timestamp, the user the event belongs to (FK to `users`), and optionally the admin who performed the action (for role/status changes initiated by another user). Indexes optimize the primary query pattern: per-user reverse chronological history with pagination (AC-3). A partial index on `event_type = 'Login'` accelerates login-specific queries. The schema also adds a `user_type` column to the existing `users` table to support role-user-type validation (edge case 2) if not already present.

## Dependent Tasks

- US_015 task_001 (requires base database schema and EF Core context with `users` table)

## Impacted Components

- New: `server/src/PropelIQ.Infrastructure/Persistence/Entities/UserActivityLog.cs` (EF entity)
- Modify: `server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs` (add DbSet and configuration)
- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/YYYYMMDD_AddUserActivityLog.cs` (migration)

## Implementation Plan

1. **Create `UserActivityLog` entity**:

```csharp
// PropelIQ.Infrastructure/Persistence/Entities/
//   UserActivityLog.cs

public sealed class UserActivityLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByName { get; set; }
}
```

Corresponding SQL schema:

```sql
CREATE TABLE user_activity_log (
    id                  uuid PRIMARY KEY
                        DEFAULT gen_random_uuid(),
    user_id             uuid NOT NULL
                        REFERENCES users(user_id)
                        ON DELETE CASCADE,
    event_type          varchar(50) NOT NULL,
    description         text NOT NULL DEFAULT '',
    occurred_at_utc     timestamptz NOT NULL
                        DEFAULT now(),
    performed_by_user_id uuid
                        REFERENCES users(user_id)
                        ON DELETE SET NULL,
    performed_by_name   varchar(200)
);

-- Primary query: per-user reverse chronological
CREATE INDEX
    ix_user_activity_log_user_date
    ON user_activity_log (
        user_id, occurred_at_utc DESC);

-- Login event fast lookup
CREATE INDEX
    ix_user_activity_log_login
    ON user_activity_log (
        user_id, occurred_at_utc DESC)
    WHERE event_type = 'Login';
```

2. **Add `user_type` column** to `users` table (if not present) for role-user-type validation:

```sql
-- Only if column does not exist
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS
        user_type varchar(50)
        NOT NULL DEFAULT 'Staff';
```

3. **Register DbSet** in `AppDbContext`:

```csharp
// In AppDbContext.cs — add:
public DbSet<UserActivityLog>
    UserActivityLogs =>
        Set<UserActivityLog>();
```

4. **Configure entity mapping** with EF Core Fluent API:

```csharp
// In OnModelCreating or separate
// IEntityTypeConfiguration class:

builder.Entity<UserActivityLog>(entity =>
{
    entity.ToTable("user_activity_log");
    entity.HasKey(e => e.Id);

    entity.Property(e => e.EventType)
        .HasMaxLength(50)
        .IsRequired();

    entity.Property(e => e.Description)
        .IsRequired()
        .HasDefaultValue("");

    entity.Property(e => e.PerformedByName)
        .HasMaxLength(200);

    entity.HasIndex(
        e => new { e.UserId, e.OccurredAtUtc })
        .IsDescending(false, true);

    entity.HasOne<User>()
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne<User>()
        .WithMany()
        .HasForeignKey(e => e.PerformedByUserId)
        .OnDelete(DeleteBehavior.SetNull);
});
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        └── PropelIQ.Infrastructure/
            └── Persistence/
                ├── AppDbContext.cs                    (modify)
                ├── Entities/
                │   └── UserActivityLog.cs            (new)
                └── Migrations/
                    └── YYYYMMDD_AddUserActivityLog.cs (new)
```

> Placeholder: Update on execution based on US_015 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Entities/UserActivityLog.cs | Entity for user activity history with event type, description, timestamps, performer references |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add UserActivityLogs DbSet, Fluent API config, composite index, FK constraints |

## External References

- EF Core Indexes: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- EF Core Relationships: https://learn.microsoft.com/en-us/ef/core/modeling/relationships
- PostgreSQL Partial Indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- PostgreSQL ALTER TABLE: https://www.postgresql.org/docs/15/sql-altertable.html

## Build Commands

```bash
# Generate migration
cd server
dotnet ef migrations add AddUserActivityLog \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update \
  --project src/PropelIQ.Infrastructure \
  --startup-project src/PropelIQ.Api

# Verify table
psql -d propeliq -c "\d user_activity_log"
psql -d propeliq -c "\di ix_user_activity_log_*"
```

## Implementation Validation Strategy

- [x] `user_activity_log` table created with correct columns and FK constraints
- [x] Composite index on (user_id, occurred_at_utc DESC) exists for reverse chronological queries
- [x] Partial index on login events exists for fast login history lookup
- [x] CASCADE delete on user_id FK removes activity logs when user is deleted
- [x] SET NULL on performed_by_user_id FK preserves logs when performing admin is deleted
- [x] Migration applies cleanly and rolls back without errors

## Implementation Checklist

- [x] Create UserActivityLog entity with userId, eventType, description, occurredAtUtc, performedByUserId, performedByName
- [x] Add user_type column to users table if not already present
- [x] Register UserActivityLogs DbSet in AppDbContext
- [x] Configure Fluent API with FK constraints (CASCADE for user, SET NULL for performer)
- [x] Add composite descending index on (user_id, occurred_at_utc) for activity history queries
- [x] Generate and apply EF Core migration

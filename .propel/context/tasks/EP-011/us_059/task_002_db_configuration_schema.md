# Task - TASK_002

## Requirement Reference

- User Story: us_059
- Story Location: .propel/context/tasks/EP-011/us_059/us_059.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I update a system configuration, Then the change is validated, saved with a version number and timestamp, and takes effect for new events from that point forward.
  - AC-3: Given I want to review configuration history, When I open the configuration version history, Then all previous versions are listed with the change date, changed by (admin identity), and the before/after values.
  - AC-4: Given a configuration rollback is needed, When I select a previous version and click "Restore," Then the previous configuration is reapplied as a new version (not an overwrite) and takes effect immediately.
- Edge Cases:
  - What happens if two admins change the same configuration simultaneously? Optimistic concurrency control detects the conflict; the second admin is shown the current value and must confirm or cancel their change.

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
| Database | PostgreSQL with pgvector | 15.x |
| Library | EF Core | 8.x |
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

Create the database schema and EF Core migration for the versioned configuration storage system. The `configuration_versions` table is the sole persistence layer for all system configuration — it follows an append-only (insert-only) pattern where every change creates a new row with an auto-incremented version number, ensuring full auditability and rollback capability (AC-1, AC-3, AC-4). The table stores a `category` column matching the four FR-AD-001 domains (slot_templates, reminder_rules, session_policy, communication_templates), a `values` JSONB column holding the configuration snapshot, a `diff` JSONB column recording before/after changes for the history view (AC-3), `version_number` for sequential ordering, `updated_by_user_id` for admin identity tracking, and `restored_from_version_id` as a nullable self-reference for rollback traceability (AC-4). A `xmin` system column is leveraged for PostgreSQL-native optimistic concurrency control (edge case 1). Composite indexes support the primary query patterns: latest version per category (for cache population), version history per category (for the history endpoint), and conflict detection. Seed data provides version 1 for each category with system defaults.

## Dependent Tasks

- US_015 task_001 (requires `users` table for `updated_by_user_id` foreign key)

## Impacted Components

- New: `server/src/PropelIQ.Domain/Configuration/ConfigurationVersion.cs` (entity)
- New: `server/src/PropelIQ.Infrastructure/Persistence/Migrations/<timestamp>_AddConfigurationVersionSchema.cs` (EF Core migration)
- Modify: `server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs` (add DbSet and Fluent API configuration)

## Implementation Plan

1. **Create `ConfigurationVersion` entity** in the Domain layer:

```csharp
// server/src/PropelIQ.Domain/Configuration/
//   ConfigurationVersion.cs
namespace PropelIQ.Domain.Configuration;

public sealed class ConfigurationVersion
{
    public Guid Id { get; set; }
    public string Category { get; set; } = default!;
    public int VersionNumber { get; set; }
    public Dictionary<string, object> Values
        { get; set; } = new();
    public Dictionary<string, object>? Diff
        { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public string UpdatedByName { get; set; }
        = default!;
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? RestoredFromVersionId { get; set; }
    public ConfigurationVersion?
        RestoredFromVersion { get; set; }
    public uint RowVersion { get; set; }
}
```

2. **Configure Fluent API in AppDbContext** for the `configuration_versions` table:

```csharp
// In AppDbContext.OnModelCreating
modelBuilder.Entity<ConfigurationVersion>(e =>
{
    e.ToTable("configuration_versions",
        schema: "app");
    e.HasKey(x => x.Id);

    e.Property(x => x.Category)
        .HasMaxLength(50)
        .IsRequired();

    e.Property(x => x.VersionNumber)
        .IsRequired();

    e.Property(x => x.Values)
        .HasColumnType("jsonb")
        .IsRequired();

    e.Property(x => x.Diff)
        .HasColumnType("jsonb");

    e.Property(x => x.UpdatedByUserId)
        .IsRequired();

    e.Property(x => x.UpdatedByName)
        .HasMaxLength(200)
        .IsRequired();

    e.Property(x => x.UpdatedAtUtc)
        .IsRequired()
        .HasDefaultValueSql("now()");

    e.HasOne(x => x.RestoredFromVersion)
        .WithMany()
        .HasForeignKey(
            x => x.RestoredFromVersionId)
        .OnDelete(DeleteBehavior.SetNull);

    e.Property(x => x.RowVersion)
        .IsRowVersion();

    e.HasIndex(x => new
        { x.Category, x.VersionNumber })
        .IsUnique()
        .HasDatabaseName(
            "ix_config_versions_category_version");

    e.HasIndex(x => new
        { x.Category, x.UpdatedAtUtc })
        .IsDescending(false, true)
        .HasDatabaseName(
            "ix_config_versions_category_updated");
});
```

3. **Create EF Core migration** producing the following SQL:

```sql
CREATE TABLE app.configuration_versions (
    id                       uuid PRIMARY KEY
                             DEFAULT gen_random_uuid(),
    category                 varchar(50) NOT NULL,
    version_number           int NOT NULL,
    values                   jsonb NOT NULL,
    diff                     jsonb,
    updated_by_user_id       uuid NOT NULL
                             REFERENCES app.users(id),
    updated_by_name          varchar(200) NOT NULL,
    updated_at_utc           timestamptz NOT NULL
                             DEFAULT now(),
    restored_from_version_id uuid
                             REFERENCES
                             app.configuration_versions(id)
                             ON DELETE SET NULL,
    xmin                     xid
);

-- Unique constraint: one version number per category
CREATE UNIQUE INDEX ix_config_versions_category_version
    ON app.configuration_versions
    (category, version_number);

-- History query: latest versions per category
CREATE INDEX ix_config_versions_category_updated
    ON app.configuration_versions
    (category, updated_at_utc DESC);
```

4. **Seed default configuration** as version 1 for each category:

```csharp
// In migration Up() method — seed data
migrationBuilder.InsertData(
    table: "configuration_versions",
    schema: "app",
    columns: new[]
    {
        "id", "category", "version_number",
        "values", "updated_by_user_id",
        "updated_by_name", "updated_at_utc"
    },
    values: new object[,]
    {
        {
            Guid.NewGuid(), "SlotTemplates", 1,
            @"{""defaultDurationMinutes"": 30,
               ""bufferMinutes"": 5,
               ""maxDailySlots"": 40}",
            systemAdminId, "System",
            DateTime.UtcNow
        },
        {
            Guid.NewGuid(), "ReminderRules", 1,
            @"{""cadenceHours"": 24,
               ""maxReminders"": 3,
               ""escalationThresholdHours"": 72}",
            systemAdminId, "System",
            DateTime.UtcNow
        },
        {
            Guid.NewGuid(), "SessionPolicy", 1,
            @"{""timeoutMinutes"": 15,
               ""warningLeadMinutes"": 2,
               ""maxConcurrentSessions"": 1}",
            systemAdminId, "System",
            DateTime.UtcNow
        },
        {
            Guid.NewGuid(),
            "CommunicationTemplates", 1,
            @"{""defaultSender"":
                ""noreply@propeliq.com"",
               ""replyTo"":
                ""support@propeliq.com"",
               ""footerText"":
                ""PropelIQ Healthcare Platform""}",
            systemAdminId, "System",
            DateTime.UtcNow
        }
    });
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Domain/
        │   └── Configuration/
        │       └── ConfigurationVersion.cs              (new)
        └── PropelIQ.Infrastructure/
            └── Persistence/
                ├── AppDbContext.cs                       (modify)
                └── Migrations/
                    └── <timestamp>_AddConfigurationVersionSchema.cs (new)
```

> Placeholder: Update on execution based on US_015 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Domain/Configuration/ConfigurationVersion.cs | Entity with category, version_number, JSONB values/diff, admin identity, restored_from reference |
| CREATE | server/src/PropelIQ.Infrastructure/Persistence/Migrations/*_AddConfigurationVersionSchema.cs | Migration creating table, unique index, history index, seed data for 4 categories |
| MODIFY | server/src/PropelIQ.Infrastructure/Persistence/AppDbContext.cs | Add DbSet and Fluent API configuration with JSONB columns and row version |

## External References

- EF Core JSONB with Npgsql: https://www.npgsql.org/efcore/mapping/json.html
- PostgreSQL xmin for OCC: https://www.postgresql.org/docs/15/ddl-system-columns.html
- EF Core Row Version: https://learn.microsoft.com/en-us/ef/core/saving/concurrency#resolving-concurrency-conflicts
- EF Core Data Seeding: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding

## Build Commands

```bash
# Generate migration
cd server
dotnet ef migrations add AddConfigurationVersionSchema \
    --project src/PropelIQ.Infrastructure \
    --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update \
    --project src/PropelIQ.Infrastructure \
    --startup-project src/PropelIQ.Api

# Verify table and seed data
psql -d propeliq -c \
    "SELECT category, version_number FROM app.configuration_versions ORDER BY category"
```

## Implementation Validation Strategy

- [ ] Table created with correct columns, types, JSONB for values and diff
- [ ] Unique index on (category, version_number) prevents duplicate versions
- [ ] History index on (category, updated_at_utc DESC) supports version listing
- [ ] Self-referencing FK for restored_from_version_id with SET NULL on delete
- [ ] Seed data creates version 1 for all 4 categories with valid defaults
- [ ] Migration is reversible with proper rollback support

## Implementation Checklist

- [x] Create ConfigurationVersion entity with JSONB values/diff, version number, and admin tracking
- [x] Configure Fluent API with table mapping, JSONB columns, row version, and self-referencing FK
- [x] Create unique composite index on (category, version_number)
- [x] Create descending history index on (category, updated_at_utc)
- [x] Seed version 1 for SlotTemplates, ReminderRules, SessionPolicy, CommunicationTemplates
- [x] Generate and apply EF Core migration with rollback support

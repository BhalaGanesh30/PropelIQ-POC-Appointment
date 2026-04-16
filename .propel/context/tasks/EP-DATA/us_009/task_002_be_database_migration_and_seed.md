# Task - TASK_002

## Requirement Reference

- User Story: us_009
- Story Location: .propel/context/tasks/EP-DATA/us_009/us_009.md
- Acceptance Criteria:
  - AC-1: Given the solution is built, When `dotnet ef migrations add InitialSchema` is executed, Then a migration is generated covering all entities: User, Patient, Appointment, WaitlistEntry, ReminderEvent, InsuranceProfile, ClinicalDocument, ClinicalFact, CodingDecision, and AuditRecord.
  - AC-4: Given seed data scripts are present, When `dotnet ef database update` is run against a fresh database, Then reference data and mock patient records are seeded without errors.
- Edge Case:
  - What happens if a migration is applied to a database with existing data that violates a new constraint? Migration includes a data fix step or is blocked with an explicit pre-migration check.
  - How does the system handle entity model changes mid-sprint? New migration is added additively; old migration is not altered to preserve zero-downtime rollout support per DR-007.

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
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Library | Microsoft.EntityFrameworkCore.Design | 8.x |
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x |
| Library | Microsoft.EntityFrameworkCore.Tools | 8.x |
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

Generate the `InitialSchema` EF Core migration from the fully-configured entity model (task_001) and apply it to a fresh PostgreSQL database running via Docker Compose. Configure seed data using EF Core's `UseSeeding` / `UseAsyncSeeding` callbacks to insert reference data (appointment types, role definitions) and mock patient records. Implement a pre-migration constraint check pattern: before applying any migration that introduces a new unique constraint or NOT NULL column to a populated table, a guard SQL step verifies existing data compatibility and raises an explicit error if violations are found. Document the additive migration convention (DR-007) and verify migration rollback with `dotnet ef database update <PreviousMigration>`.

## Dependent Tasks

- task_001_be_domain_entity_models (requires all 10 entities configured in AppDbContext)
- US_003 task_001 (requires PostgreSQL container running and accessible)

## Impacted Components

- New: `server/src/SharedServices.Infrastructure/Persistence/Migrations/` (generated migration files)
- New: `server/src/SharedServices.Infrastructure/Persistence/Seed/AppDbContextSeed.cs` (seed logic)
- Modify: `server/src/SharedServices.Infrastructure/Persistence/AppDbContext.cs` (wire `UseSeeding`)
- New: `server/src/SharedServices.Infrastructure/Persistence/DesignTimeAppDbContextFactory.cs` (already created in US_003 — verify exists)

## Implementation Plan

1. **Verify `DesignTimeDbContextFactory`** exists from US_003. If not, create it to allow EF Core CLI tools to instantiate `AppDbContext` without running the full application host:

```csharp
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder
            .UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")
                    ?? "Host=localhost;Port=5432;Database=propeliq;Username=app_user;Password=app_pass",
                o => o.MigrationsHistoryTable("__ef_migrations_history", "app"))
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}
```

2. **Generate the initial migration** by running the EF Core CLI tool targeting the Infrastructure project with the API as startup project:

```bash
dotnet ef migrations add InitialSchema \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations \
  --namespace PropelIQ.SharedServices.Infrastructure.Persistence.Migrations
```

This generates three files: `<timestamp>_InitialSchema.cs`, `<timestamp>_InitialSchema.Designer.cs`, and `AppDbContextModelSnapshot.cs`. Verify the generated migration includes `CREATE TABLE` statements for all 10 entities, JSONB columns for `contact_preferences` and `details`, UUID columns with `DEFAULT gen_random_uuid()`, and appropriate foreign key constraints.

3. **Implement the pre-migration constraint check pattern** (edge case). For any future migration introducing a new NOT NULL column or unique constraint, add a guard SQL step before the schema change:

```csharp
// Example: In a future migration adding unique constraint to existing table
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Pre-migration guard: check for violations before applying unique constraint
    migrationBuilder.Sql(@"
        DO $$
        BEGIN
            IF EXISTS (
                SELECT mrn, COUNT(*)
                FROM app.patients
                GROUP BY mrn
                HAVING COUNT(*) > 1
            ) THEN
                RAISE EXCEPTION
                    'Migration blocked: duplicate MRN values exist. '
                    'Run data fix script before applying migration.';
            END IF;
        END $$;
    ");

    // Constraint applied only if guard passes
    migrationBuilder.CreateIndex(
        name: "ix_patients_mrn",
        schema: "app",
        table: "patients",
        column: "mrn",
        unique: true);
}
```

Document this pattern in a `MIGRATION_CONVENTIONS.md` in the Migrations folder as the project convention per DR-007.

4. **Create `AppDbContextSeed.cs`** with reference data and mock patient records using EF Core's idempotent seeding pattern. Seed data checks for existence before inserting to support repeated `dotnet ef database update` runs:

```csharp
public static class AppDbContextSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        // Reference data: default admin user
        if (!await context.Users.AnyAsync(u => u.Email == "admin@propeliq.local", ct))
        {
            context.Users.Add(new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Email = "admin@propeliq.local",
                PasswordHash = "CHANGE_ME_BEFORE_PRODUCTION",
                Role = "Admin",
                FirstName = "System",
                LastName = "Admin"
            });
        }

        // Mock patient record (development only)
        if (!await context.Patients.AnyAsync(p => p.MRN == "MOCK-001", ct))
        {
            var mockUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            context.Users.Add(new User
            {
                Id = mockUserId,
                Email = "mock.patient@propeliq.local",
                PasswordHash = "CHANGE_ME_BEFORE_PRODUCTION",
                Role = "Patient",
                FirstName = "Mock",
                LastName = "Patient"
            });
            context.Patients.Add(new Patient
            {
                UserId = mockUserId,
                FirstName = "Mock",
                LastName = "Patient",
                DateOfBirth = new DateOnly(1985, 6, 15),
                MRN = "MOCK-001",
                ContactPreferences = new ContactPreferences
                {
                    SmsEnabled = true,
                    EmailEnabled = true,
                    PreferredLanguage = "en"
                }
            });
        }

        await context.SaveChangesAsync(ct);
    }
}
```

5. **Wire seed into `AppDbContext`** using `UseSeeding` / `UseAsyncSeeding` so seed runs automatically during `dotnet ef database update` (AC-4):

```csharp
// In DbContextOptionsBuilder registration (Program.cs or DI extension)
options.UseNpgsql(connectionString, ...)
       .UseSnakeCaseNamingConvention()
       .UseAsyncSeeding(async (context, _, ct) =>
           await AppDbContextSeed.SeedAsync((AppDbContext)context, ct));
```

6. **Apply the migration to the running Docker Compose PostgreSQL instance**:

```bash
dotnet ef database update \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api
```

Verify the `app.__ef_migrations_history` table records the `InitialSchema` migration entry, and inspect key tables (`app.users`, `app.patients`, `app.audit_records`) via `psql` or a database client.

7. **Verify migration rollback** per DR-007 convention (additive migration support):

```bash
# Roll back to empty state
dotnet ef database update 0 \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Reapply
dotnet ef database update \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api
```

Confirm the `Down()` method in the generated migration correctly drops all tables in reverse dependency order (no FK constraint violation on rollback).

8. **Document the additive migration convention** in `server/src/SharedServices.Infrastructure/Persistence/Migrations/MIGRATION_CONVENTIONS.md`. Key rules per DR-007:
   - Never modify an existing migration that has been applied to any environment
   - New changes always generate a new migration (`dotnet ef migrations add <DescriptiveName>`)
   - Destructive changes (column removal) require a two-migration strategy: first make nullable, then remove in next sprint migration
   - Pre-migration constraint guards are mandatory for any migration adding a unique or NOT NULL constraint to an existing table

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       ├── PropelIQ.Api/
│       │   └── appsettings.json
│       └── SharedServices.Infrastructure/
│           └── Persistence/
│               ├── AppDbContext.cs        (with 10 DbSets from task_001)
│               └── DesignTimeAppDbContextFactory.cs
├── docker-compose.yml
└── .env.example
```

> Placeholder: Update on execution based on task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/SharedServices.Infrastructure/Persistence/Migrations/\<timestamp\>_InitialSchema.cs | Generated EF Core migration covering all 10 entities |
| CREATE | server/src/SharedServices.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs | EF Core model snapshot (auto-generated) |
| CREATE | server/src/SharedServices.Infrastructure/Persistence/Seed/AppDbContextSeed.cs | Idempotent seed: admin user, mock patient record |
| CREATE | server/src/SharedServices.Infrastructure/Persistence/Migrations/MIGRATION_CONVENTIONS.md | DR-007 additive migration rules documentation |
| MODIFY | server/src/SharedServices.Infrastructure/Persistence/AppDbContext.cs | Wire UseAsyncSeeding to AppDbContextSeed.SeedAsync |

## External References

- `dotnet ef migrations add` CLI reference: https://learn.microsoft.com/en-us/ef/core/cli/dotnet#dotnet-ef-migrations-add
- `dotnet ef database update` CLI reference: https://learn.microsoft.com/en-us/ef/core/cli/dotnet#dotnet-ef-database-update
- EF Core data seeding (UseSeeding / HasData): https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
- EF Core design-time DbContext creation: https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation
- EF Core migrations overview: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- Npgsql schema management: https://www.npgsql.org/efcore/modeling/schemas.html
- PostgreSQL DO block (PL/pgSQL): https://www.postgresql.org/docs/15/sql-do.html
- DR-007 (zero-downtime schema migration): .propel/context/docs/design.md

## Build Commands

```bash
# Generate initial migration
dotnet ef migrations add InitialSchema \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations

# Check for pending model changes (EF Core 8+)
dotnet ef migrations has-pending-model-changes \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Apply migration + seed data
dotnet ef database update \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Rollback to empty state
dotnet ef database update 0 \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Inspect applied migrations
dotnet ef migrations list \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] `dotnet ef migrations add InitialSchema` generates migration covering all 10 entity tables (AC-1)
- [ ] Generated migration contains UUID (`uuid`) columns with `gen_random_uuid()` defaults for all PKs (AC-2)
- [ ] Generated migration contains `jsonb` column type for `contact_preferences` and `details` columns (AC-3)
- [ ] `dotnet ef database update` applies migration and seeds admin user and mock patient without errors (AC-4)
- [ ] `app.__ef_migrations_history` table contains `InitialSchema` entry after apply
- [ ] `dotnet ef database update 0` rolls back all tables without FK constraint violations (edge case)
- [ ] Pre-migration guard SQL pattern documented and tested with a simulated data violation scenario (edge case)
- [ ] `dotnet ef migrations has-pending-model-changes` returns no pending changes after migration is generated

## Implementation Checklist

- [ ] Verify or create `DesignTimeAppDbContextFactory` pointing to local PostgreSQL connection string
- [ ] Run `dotnet ef migrations add InitialSchema` and confirm all 10 tables appear in the generated `Up()` method
- [ ] Inspect generated migration for `uuid` PKs, `jsonb` column types, `unique` indexes, and FK constraints
- [ ] Create `AppDbContextSeed.cs` with idempotent seed logic for admin user and mock patient record
- [ ] Wire `UseAsyncSeeding` in `AppDbContext` options to call `AppDbContextSeed.SeedAsync`
- [ ] Run `dotnet ef database update` against Docker Compose PostgreSQL and verify tables and seed data
- [ ] Run `dotnet ef database update 0` and confirm clean rollback without errors
- [ ] Create `MIGRATION_CONVENTIONS.md` documenting additive-only migration rules per DR-007

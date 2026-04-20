# Task - TASK_002

## Requirement Reference

- User Story: us_003
- Story Location: .propel/context/tasks/EP-TECH/us_003/us_003.md
- Acceptance Criteria:
  - AC-3: Given the database is provisioned, When Entity Framework Core migrations are run with `dotnet ef database update`, Then all migrations apply without error and the schema matches the defined entities.
  - AC-4: Given the application is connected to the database, When a test query is executed over a vector column, Then the query completes successfully using the pgvector operator (`<->`).
- Edge Case:
  - How does the system handle schema migration failure? Migration rollback is supported; failed migration is reported with the exact SQL statement that failed.

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
| Library | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x |
| Library | Npgsql.EntityFrameworkCore.PostgreSQL.Design | 8.x |
| Library | pgvector (Pgvector.EntityFrameworkCore) | latest stable |
| Library | Microsoft.EntityFrameworkCore.Design | 8.x |
| AI/ML | N/A | N/A |
| Vector Store | pgvector | 0.7.x |
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

Configure Entity Framework Core 8 with the Npgsql PostgreSQL provider and pgvector support across the modular solution. Create a shared `AppDbContext` in the SharedServices Infrastructure layer, register the pgvector extension in the model (`HasPostgresExtension("vector")`), add a sample entity with a vector column to validate the pgvector `<->` distance operator, configure design-time DbContext factory for migration tooling, generate and apply the initial migration, and verify migration rollback support. This task delivers the ORM and migration foundation required by DR-001 and DR-003.

## Dependent Tasks

- task_001_db_postgresql_provisioning (requires running PostgreSQL 15 + pgvector instance)
- US_002 task_001_be_aspnet_solution_scaffold (requires compiled modular solution structure)

## Impacted Components

- Modified: `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/PropelIQ.Modules.SharedServices.Infrastructure.csproj` (Npgsql and pgvector NuGet packages)
- New: `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/AppDbContext.cs` (shared DbContext)
- New: `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/DesignTimeDbContextFactory.cs` (migration tooling support)
- New: `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/Configurations/` (entity type configurations)
- Modified: `server/src/PropelIQ.Api/Program.cs` (DbContext registration with Npgsql provider)
- Modified: `server/src/PropelIQ.Api/PropelIQ.Api.csproj` (EF Core Design package for migrations)
- Modified: `server/src/PropelIQ.Api/appsettings.json` (connection string configuration)
- New: `server/src/PropelIQ.SharedKernel/BaseEntity.cs` (enhanced with audit fields if not already present)

## Implementation Plan

1. **Install NuGet packages** in the SharedServices.Infrastructure project:
   - `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x (Npgsql EF Core provider)
   - `Pgvector.EntityFrameworkCore` (pgvector EF Core integration for vector column mapping)
   - `Microsoft.EntityFrameworkCore.Design` 8.x in the Api project (migration tooling)

2. **Create `AppDbContext`** in the SharedServices.Infrastructure Data directory. Register PostgreSQL extensions in `OnModelCreating`:

### EF Core 8 DbContext with pgvector Extension Registration

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register PostgreSQL extensions (generates CREATE EXTENSION in migrations)
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Apply entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
```

Source: Npgsql EF Core provider - HasPostgresExtension documentation

3. **Create a sample entity with vector column** to validate pgvector integration. Use the `Pgvector` NuGet type for the vector property and configure the column with `HasColumnType("vector(1536)")` for OpenAI-compatible embedding dimensions (aligned with AIR-004 retrieval workloads):

### Entity with pgvector Column Configuration

```csharp
public class EmbeddingSample
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
}

// In IEntityTypeConfiguration<EmbeddingSample>
builder.Property(e => e.Embedding)
    .HasColumnType("vector(1536)");

builder.HasIndex(e => e.Embedding)
    .HasMethod("ivfflat")
    .HasOperators("vector_cosine_ops");
```

Source: Pgvector.EntityFrameworkCore integration guide

4. **Create `DesignTimeDbContextFactory`** implementing `IDesignTimeDbContextFactory<AppDbContext>` to enable `dotnet ef` CLI tooling without requiring the application to run:

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(),
                "../PropelIQ.Api"))
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            o => o.UseVector());

        return new AppDbContext(optionsBuilder.Options);
    }
}
```

5. **Register DbContext in `Program.cs`** with the Npgsql provider and pgvector support:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.UseVector()));
```

6. **Configure connection string** in `appsettings.json` and `appsettings.Development.json` referencing the Docker-hosted PostgreSQL instance.

7. **Generate and apply the initial migration**:
   ```bash
   dotnet ef migrations add InitialCreate \
     --project server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
     --startup-project server/src/PropelIQ.Api
   dotnet ef database update \
     --project server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
     --startup-project server/src/PropelIQ.Api
   ```

8. **Validate pgvector operator** by executing a test query using the `<->` (L2 distance) operator against the sample vector column. This can be a simple integration test or a seed data verification script.

## Current Project State

```text
server/
├── PropelIQ.sln
├── src/
│   ├── PropelIQ.Api/
│   │   ├── PropelIQ.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── PropelIQ.SharedKernel/
│   │   └── BaseEntity.cs
│   └── Modules/
│       └── SharedServices/
│           └── PropelIQ.Modules.SharedServices.Infrastructure/
│               └── PropelIQ.Modules.SharedServices.Infrastructure.csproj
docker-compose.yml                  (PostgreSQL from task_001)
docker/postgres/init/               (init scripts from task_001)
.env.example
```

> Assumes us_002/task_001 and us_003/task_001 are completed. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/PropelIQ.Modules.SharedServices.Infrastructure.csproj | Add Npgsql.EntityFrameworkCore.PostgreSQL and Pgvector.EntityFrameworkCore NuGet packages |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/AppDbContext.cs | Shared DbContext with pgvector and uuid-ossp extension registration |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/DesignTimeDbContextFactory.cs | IDesignTimeDbContextFactory for dotnet ef CLI tooling |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/Configurations/EmbeddingSampleConfiguration.cs | Vector column entity type configuration |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Domain/Entities/EmbeddingSample.cs | Sample entity with vector property for pgvector validation |
| MODIFY | server/src/PropelIQ.Api/PropelIQ.Api.csproj | Add Microsoft.EntityFrameworkCore.Design package |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register AppDbContext with UseNpgsql and UseVector |
| MODIFY | server/src/PropelIQ.Api/appsettings.json | Add ConnectionStrings:DefaultConnection placeholder |
| MODIFY | server/src/PropelIQ.Api/appsettings.Development.json | Add development connection string pointing to Docker PostgreSQL |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Migrations/ | Generated EF Core migration files from InitialCreate |

## External References

- Npgsql EF Core provider (PostgreSQL): https://www.npgsql.org/efcore/index.html
- Npgsql EF Core HasPostgresExtension: https://www.npgsql.org/efcore/modeling/general.html
- Pgvector.EntityFrameworkCore integration: https://github.com/pgvector/pgvector-dotnet#entity-framework-core
- EF Core 8 migrations CLI: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli
- EF Core 8 DbContext configuration: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
- EF Core design-time DbContext factory: https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation
- pgvector index types (IVFFlat, HNSW): https://github.com/pgvector/pgvector#indexing
- pgvector distance operators (`<->` L2, `<=>` cosine, `<#>` inner product): https://github.com/pgvector/pgvector#querying

## Build Commands

```bash
# Build solution
dotnet build server/PropelIQ.sln

# Ensure PostgreSQL is running
docker compose up -d postgres

# Add initial migration
dotnet ef migrations add InitialCreate \
  --project server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Apply migration
dotnet ef database update \
  --project server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Rollback migration (verify rollback support)
dotnet ef database update 0 \
  --project server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Verify vector query (psql)
docker compose exec postgres psql -U propeliq_user -d propeliq \
  -c "SELECT '[1,2,3]'::vector <-> '[4,5,6]'::vector AS distance;"
```

## Implementation Validation Strategy

- [ ] `dotnet build server/PropelIQ.sln` compiles with zero errors after package additions
- [ ] `dotnet ef migrations add InitialCreate` generates migration files without errors
- [ ] `dotnet ef database update` applies migration and creates tables matching entity definitions
- [ ] Database schema contains `vector` extension (verify via `pg_extension`)
- [ ] Sample entity table has a `vector(1536)` column type
- [ ] pgvector `<->` distance operator query completes successfully against the vector column
- [ ] `dotnet ef database update 0` rolls back migration cleanly
- [ ] Re-applying migration after rollback succeeds without errors

## Implementation Checklist

- [x] Install `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x and `Pgvector.EntityFrameworkCore` in SharedServices.Infrastructure project
- [x] Install `Microsoft.EntityFrameworkCore.Design` 8.x in PropelIQ.Api project
- [x] Create `AppDbContext` with `HasPostgresExtension("vector")`, `HasPostgresExtension("uuid-ossp")`, and assembly-based configuration application
- [x] Create `DesignTimeDbContextFactory` implementing `IDesignTimeDbContextFactory<AppDbContext>` for CLI migration support
- [x] Create sample `EmbeddingSample` entity with `Vector` property and IVFFlat index configuration
- [x] Register `AppDbContext` in `Program.cs` with `UseNpgsql()` and `UseVector()`
- [x] Generate initial migration with `dotnet ef migrations add InitialCreate` and apply with `dotnet ef database update`
- [ ] Validate pgvector `<->` operator query and migration rollback support

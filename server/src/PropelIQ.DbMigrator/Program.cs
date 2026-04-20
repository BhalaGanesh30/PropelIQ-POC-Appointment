using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PropelIQ.DbMigrator;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

// ─────────────────────────────────────────────────────────────────────────────
// PropelIQ.DbMigrator
//
// Standalone executable that applies all EF Core migrations to the target
// PostgreSQL database. Run locally, in Docker, or from CI/CD pipelines.
//
// Connection string resolution order (first wins):
//   1. Environment variable  DATABASE_URL
//   2. Environment variable  ConnectionStrings__Default
//   3. appsettings.json      ConnectionStrings:Default
//
// Usage:
//   dotnet run --project src/PropelIQ.DbMigrator
//   DATABASE_URL="Host=...;Database=propeliq;Username=postgres;Password=..." dotnet run ...
// ─────────────────────────────────────────────────────────────────────────────

// ── Configuration ─────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? config.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No connection string found. " +
        "Set DATABASE_URL env var or ConnectionStrings:Default in appsettings.json.");

// ── Logging ───────────────────────────────────────────────────────────────────
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddConsole().SetMinimumLevel(LogLevel.Information));

var log = loggerFactory.CreateLogger("DbMigrator");

log.LogInformation("PropelIQ DbMigrator starting…");
log.LogInformation("Target: {Host}/{Db}",
    new NpgsqlConnectionStringBuilder(connectionString).Host,
    new NpgsqlConnectionStringBuilder(connectionString).Database);

// ── Pre-flight: ensure schemas and extensions ─────────────────────────────────
// Schemas and extensions must exist before EF migrations run because the
// InitialCreate migration generates CREATE EXTENSION statements that require
// prior schema ownership in some PostgreSQL configurations.
try
{
    await EnsurePrerequisitesAsync(connectionString, log);
}
catch (Exception ex)
{
    log.LogWarning("Pre-flight step encountered an issue (non-fatal): {Message}", ex.Message);
}

// ── EF Core migrations ────────────────────────────────────────────────────────
var services = new ServiceCollection();

services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsql =>
        {
            // Register vector type mapping only when pgvector extension is available.
            // The DbMigrator pre-flight check already tested extension availability.
            try { npgsql.UseVector(); } catch { /* pgvector not installed — skip */ }
            npgsql.MigrationsHistoryTable("__ef_migrations_history", "app");
        })
    .UseSnakeCaseNamingConvention()
    // tenant_id columns are added via raw SQL in migrations, not via EF model properties.
    // Suppress the resulting PendingModelChangesWarning — the divergence is intentional.
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
    // Intercept extension creation commands so missing optional extensions (pgvector,
    // pgaudit) are handled gracefully rather than failing the entire migration run.
    .AddInterceptors(new ExtensionAvailabilityInterceptor())
    .UseLoggerFactory(loggerFactory));

await using var serviceProvider = services.BuildServiceProvider();
await using var scope = serviceProvider.CreateAsyncScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

// List pending migrations before applying
var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count == 0)
{
    log.LogInformation("No pending migrations. Database is up to date.");
}
else
{
    log.LogInformation("Applying {Count} pending migration(s):", pending.Count);
    foreach (var m in pending) log.LogInformation("  → {Migration}", m);

    await dbContext.Database.MigrateAsync();

    log.LogInformation("All migrations applied successfully.");
}

// Report applied migrations
var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
log.LogInformation("Applied migrations total: {Count}", applied.Count);
foreach (var m in applied) log.LogInformation("  ✓ {Migration}", m);

log.LogInformation("PropelIQ DbMigrator complete.");

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static async Task EnsurePrerequisitesAsync(string connectionString, ILogger log)
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    // ── Schemas ───────────────────────────────────────────────────────────────
    log.LogInformation("Ensuring schemas: app, audit, compliance…");
    await ExecAsync(conn, """
        CREATE SCHEMA IF NOT EXISTS app;
        CREATE SCHEMA IF NOT EXISTS audit;
        CREATE SCHEMA IF NOT EXISTS compliance;
        """);

    // ── Extensions (each attempted independently so one failure doesn't block others) ──
    foreach (var ext in new[] { "\"uuid-ossp\"", "pg_trgm", "pgaudit" })
    {
        try
        {
            await ExecAsync(conn, $"CREATE EXTENSION IF NOT EXISTS {ext};");
            log.LogInformation("  Extension {Ext}: OK", ext);
        }
        catch (Exception ex)
        {
            log.LogWarning("  Extension {Ext}: skipped — {Msg}", ext, ex.Message);
        }
    }

    // pgvector is optional — local PostgreSQL may not have it installed.
    try
    {
        await ExecAsync(conn, "CREATE EXTENSION IF NOT EXISTS vector;");
        log.LogInformation("  Extension vector: OK");
    }
    catch (Exception ex)
    {
        log.LogWarning(
            "  Extension vector: not installed on this PostgreSQL instance. " +
            "Vector similarity search will be unavailable. " +
            "Install pgvector from https://github.com/pgvector/pgvector or use the Docker image. " +
            "Detail: {Msg}", ex.Message);
    }

    log.LogInformation("Pre-flight checks complete.");
}

static async Task ExecAsync(NpgsqlConnection conn, string sql)
{
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
}

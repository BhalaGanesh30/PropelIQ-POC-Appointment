using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data;

/// <summary>
/// Shared application DbContext for the PropelIQ platform.
/// Placed in SharedServices.Infrastructure as the central EF Core entry point;
/// individual bounded module DbContexts will be added in their own Infrastructure
/// projects as features are implemented.
///
/// DR-001: Uses PostgreSQL 15 as primary transactional datastore.
/// DR-003: EF Core migrations manage schema evolution — no manual DDL except
///         those in docker/postgres/init/ for first-run extension setup.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // ── DbSets ────────────────────────────────────────────────────────────────
    // Sample entity for pgvector integration validation (AC-4).
    // Remove or migrate to ClinicalIntelligence module when AI embedding
    // storage is implemented in EP-AI.
    public DbSet<EmbeddingSample> EmbeddingSamples => Set<EmbeddingSample>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register PostgreSQL extensions — EF Core generates the corresponding
        // CREATE EXTENSION IF NOT EXISTS statements in the migration SQL.
        // AC-2: vector extension must be active (also ensured by 01-create-extensions.sql).
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Set default schema to 'app' — all entity tables land in the app schema
        // unless explicitly overridden by an IEntityTypeConfiguration.
        modelBuilder.HasDefaultSchema("app");

        // Apply all IEntityTypeConfiguration<T> implementations in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

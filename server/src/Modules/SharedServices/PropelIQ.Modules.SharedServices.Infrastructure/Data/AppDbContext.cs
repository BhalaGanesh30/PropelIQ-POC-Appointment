using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Domain.Entities;

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
    public DbSet<EmbeddingSample> EmbeddingSamples => Set<EmbeddingSample>();

    // Administration module entities
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<InsuranceProfile> InsuranceProfiles => Set<InsuranceProfile>();

    // Scheduling module entities
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<ReminderEvent> ReminderEvents => Set<ReminderEvent>();

    // ClinicalIntelligence module entities
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<ClinicalFact> ClinicalFacts => Set<ClinicalFact>();
    public DbSet<CodingDecision> CodingDecisions => Set<CodingDecision>();

    // SharedServices module entities
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register PostgreSQL extensions — EF Core generates the corresponding
        // CREATE EXTENSION IF NOT EXISTS statements in the migration SQL.
        // AC-2: vector extension must be active (also ensured by 01-create-extensions.sql).
        modelBuilder.HasPostgresExtension("uuid-ossp");
        //modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Set default schema to 'app' — all entity tables land in the app schema
        // unless explicitly overridden by an IEntityTypeConfiguration.
        modelBuilder.HasDefaultSchema("app");

        // Apply all IEntityTypeConfiguration<T> implementations in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

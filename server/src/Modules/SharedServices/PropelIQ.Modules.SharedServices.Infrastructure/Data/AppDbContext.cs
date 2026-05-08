using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
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

    // EP-005 US_037: Insurance validation engine reference data and audit records.
    public DbSet<InsuranceProvider> InsuranceProviders => Set<InsuranceProvider>();
    public DbSet<InsuranceValidationResult> InsuranceValidationResults => Set<InsuranceValidationResult>();

    // Scheduling module entities
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<WalkIn> WalkIns => Set<WalkIn>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<ReminderEvent> ReminderEvents => Set<ReminderEvent>();
    public DbSet<DeadLetterEvent> DeadLetterEvents => Set<DeadLetterEvent>();
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<SlotTemplate> SlotTemplates => Set<SlotTemplate>();
    public DbSet<IntakeDraft> IntakeDrafts => Set<IntakeDraft>();
    public DbSet<IntakeRecord> IntakeRecords => Set<IntakeRecord>();

    // ClinicalIntelligence module entities
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<ClinicalFact> ClinicalFacts => Set<ClinicalFact>();
    public DbSet<CodingDecision> CodingDecisions => Set<CodingDecision>();
    public DbSet<DeadLetterEntry> OcrDeadLetterQueue => Set<DeadLetterEntry>();

    // EP-007 US_046: Conflict detection entities (conflict_alerts, conflict_rules).
    public DbSet<ConflictAlert> ConflictAlerts => Set<ConflictAlert>();
    public DbSet<ConflictRule> ConflictRules => Set<ConflictRule>();

    // SharedServices module entities
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    // Scheduling audit
    public DbSet<AppointmentAuditEntry> AppointmentAuditEntries => Set<AppointmentAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register PostgreSQL extensions — EF Core generates the corresponding
        // CREATE EXTENSION IF NOT EXISTS statements in the migration SQL.
        // AC-2: vector extension must be active (also ensured by 01-create-extensions.sql).
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // US_043: Register the document_category_type PostgreSQL enum so Npgsql
        // maps DocumentCategoryType <-> document_category_type in SQL.
        modelBuilder.HasPostgresEnum<DocumentCategoryType>(
            schema: "app",
            name:   "document_category_type");

        // Set default schema to 'app' — all entity tables land in the app schema
        // unless explicitly overridden by an IEntityTypeConfiguration.
        modelBuilder.HasDefaultSchema("app");

        // Apply all IEntityTypeConfiguration<T> implementations in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

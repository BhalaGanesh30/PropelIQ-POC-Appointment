using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="ConflictAlert"/>.
/// Maps to the <c>app.conflict_alerts</c> table (task_003 migration).
/// </summary>
public sealed class ConflictAlertConfiguration : IEntityTypeConfiguration<ConflictAlert>
{
    public void Configure(EntityTypeBuilder<ConflictAlert> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.PatientId)
            .IsRequired();

        builder.Property(a => a.FactIdA)
            .IsRequired();

        // fact_id_b is nullable — drug-allergy alerts may not have a second clinical fact row.
        builder.Property(a => a.FactIdB)
            .IsRequired(false);

        builder.Property(a => a.RuleId)
            .IsRequired();

        builder.Property(a => a.ConflictType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.Description)
            .IsRequired();

        builder.Property(a => a.DrugA)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.DrugB)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Acknowledged)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.AcknowledgedBy);

        builder.Property(a => a.AcknowledgedAt);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("now()");

        // FK: patient (restrict — cannot delete patient with active conflict alerts).
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: FactIdA → ClinicalFact (cascade — remove alert when source fact deleted).
        builder.HasOne<ClinicalFact>()
            .WithMany()
            .HasForeignKey(a => a.FactIdA)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: FactIdB → ClinicalFact (cascade, nullable).
        builder.HasOne<ClinicalFact>()
            .WithMany()
            .HasForeignKey(a => a.FactIdB)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: rule (restrict — rule cannot be deleted while alerts reference it).
        builder.HasOne<ConflictRule>()
            .WithMany()
            .HasForeignKey(a => a.RuleId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: acknowledging clinician (set null if user account deleted).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.AcknowledgedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique constraint on (PatientId, FactIdA, FactIdB) — deduplication (Edge Case 2).
        builder.HasIndex(a => new { a.PatientId, a.FactIdA, a.FactIdB })
            .IsUnique()
            .HasDatabaseName("uq_conflict_alerts_pair");

        // B-tree index for GET conflicts by patient.
        builder.HasIndex(a => a.PatientId)
            .HasDatabaseName("ix_conflict_alerts_patient_id");

        // Partial index for fast unacknowledged-alert queries (AC-3 acknowledgment flow).
        builder.HasIndex(a => new { a.PatientId, a.Severity })
            .HasDatabaseName("ix_conflict_alerts_unacknowledged")
            .HasFilter("acknowledged = false");
    }
}

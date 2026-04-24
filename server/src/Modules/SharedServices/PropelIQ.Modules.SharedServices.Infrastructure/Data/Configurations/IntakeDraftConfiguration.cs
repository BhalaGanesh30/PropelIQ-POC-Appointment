using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class IntakeDraftConfiguration : IEntityTypeConfiguration<IntakeDraft>
{
    public void Configure(EntityTypeBuilder<IntakeDraft> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        // JSONB columns — stored as raw JSON in PostgreSQL
        builder.Property(d => d.FormData)
            .HasColumnType("jsonb");

        builder.Property(d => d.AiPopulatedFields)
            .HasColumnType("jsonb");

        builder.Property(d => d.Status)
            .HasConversion<int>();

        // Composite index: primary query pattern — patient + slot + status (AC-3)
        builder.HasIndex(d => new { d.PatientId, d.SlotId, d.Status })
            .HasDatabaseName("ix_intake_drafts_patient_slot_status");

        // Index on ExpiresAt for efficient cleanup queries (background service)
        builder.HasIndex(d => d.ExpiresAt)
            .HasDatabaseName("ix_intake_drafts_expires_at");
    }
}

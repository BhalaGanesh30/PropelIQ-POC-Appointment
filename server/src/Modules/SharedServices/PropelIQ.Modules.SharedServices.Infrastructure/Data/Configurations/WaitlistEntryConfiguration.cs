using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Enums;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        // Status stored as string for DB readability (mirrors AppointmentConfiguration).
        builder.Property(w => w.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(w => w.PreferredAppointmentType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(w => w.CreatedAt)
            .HasDefaultValueSql("now()");

        // ── Indexes ───────────────────────────────────────────────────────────

        // Lookup by patient for GET /api/v1/waitlist.
        builder.HasIndex(w => w.PatientId);

        // Composite: FIFO matching query (FindEligibleEntriesForSlotAsync).
        builder.HasIndex(w => new { w.Status, w.Position });

        // Composite: expiry polling query (GetExpiredOffersAsync).
        builder.HasIndex(w => new { w.Status, w.ClaimExpiresAt });

        // Partial index: speed up expiry worker queries on Offered entries only.
        // Mirrors the filtered index described in the US_030 task_001 spec.
        builder.HasIndex(w => w.ClaimExpiresAt)
            .HasDatabaseName("ix_waitlist_entries_claim_expires_at_offered")
            .HasFilter("\"Status\" = 'Offered' AND \"ClaimExpiresAt\" IS NOT NULL");

        // ── Relationships ─────────────────────────────────────────────────────

        // Cross-module FK to Administration.Patient — no EF navigation property.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(w => w.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Appointment)
            .WithOne(a => a.WaitlistEntry)
            .HasForeignKey<WaitlistEntry>(w => w.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}


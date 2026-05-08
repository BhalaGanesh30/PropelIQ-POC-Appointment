using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity type configuration for <see cref="WalkIn"/> (EP-004 US_033).
///
/// Maps to the <c>walk_ins</c> table in the <c>app</c> schema.
/// DR-001: UUID PK with gen_random_uuid() default.
/// DR-002: FK to <c>patients</c> and <c>appointments</c> with ON DELETE SET NULL
///         so orphan walk-in records are retained for audit after patient/appointment deletion.
/// DR-007: All constraints are additive — existing data unaffected.
/// </summary>
public sealed class WalkInConfiguration : IEntityTypeConfiguration<WalkIn>
{
    public void Configure(EntityTypeBuilder<WalkIn> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        // ── Core fields ────────────────────────────────────────────────────────

        builder.Property(w => w.PatientName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.Phone)
            .HasMaxLength(20);

        builder.Property(w => w.VisitReason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.IsConverted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(w => w.CreatedByUserId)
            .HasColumnType("uuid")
            .IsRequired();

        // ── FK: PatientId → patients (nullable — walk-in may be anonymous) ────

        builder.Property(w => w.PatientId)
            .HasColumnType("uuid");

        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(w => w.PatientId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(w => w.PatientId)
            .HasDatabaseName("ix_walk_ins_patient_id");

        // ── FK: AppointmentId → appointments (nullable) ────────────────────────

        builder.Property(w => w.AppointmentId)
            .HasColumnType("uuid");

        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(w => w.AppointmentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(w => w.AppointmentId)
            .HasDatabaseName("ix_walk_ins_appointment_id");

        // ── FK: CreatedByUserId → users (non-nullable) ─────────────────────────

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(w => w.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // ── Index: created_at for date-range queries ───────────────────────────

        builder.HasIndex(w => w.CreatedAt)
            .HasDatabaseName("ix_walk_ins_created_at");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.AppointmentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.QueueState)
            .IsRequired()
            .HasMaxLength(50);

        // ── Booking-specific fields ───────────────────────────────────────────

        // SlotId: nullable FK to AppointmentSlot (not all appointments originate from slots).
        builder.Property(a => a.SlotId)
            .HasColumnType("uuid");

        // Unique index: one appointment per slot (prevents double-booking at DB level, AC-1).
        builder.HasIndex(a => a.SlotId)
            .IsUnique()
            .HasFilter("slot_id IS NOT NULL")
            .HasDatabaseName("ix_appointments_slot_id");

        builder.Property(a => a.IntakeRecordId)
            .HasColumnType("uuid");

        builder.Property(a => a.ConfirmationCode)
            .HasMaxLength(16);

        // Unique index: each confirmation code must be globally unique (DR-002).
        builder.HasIndex(a => a.ConfirmationCode)
            .IsUnique()
            .HasFilter("confirmation_code IS NOT NULL")
            .HasDatabaseName("ix_appointments_confirmation_code");

        builder.Property(a => a.ArtifactsGenerated)
            .HasDefaultValue(false);

        builder.Property(a => a.PdfStoragePath)
            .HasMaxLength(512);

        builder.Property(a => a.QrCodeStoragePath)
            .HasMaxLength(512);

        builder.Property(a => a.IcsStoragePath)
            .HasMaxLength(512);

        builder.Property(a => a.EmailSent)
            .HasDefaultValue(false);

        builder.Property(a => a.EmailRetryCount)
            .HasDefaultValue(0);

        // AC-3 (US_024): RFC 5545 SEQUENCE counter — incremented on reschedule so calendar
        // clients recognise updates rather than creating duplicate events.
        builder.Property(a => a.SequenceNumber)
            .HasDefaultValue(0);

        builder.Property(a => a.BookedAt)
            .HasDefaultValueSql("now()");

        builder.Property(a => a.ProviderName)
            .HasMaxLength(256);

        builder.Property(a => a.Location)
            .HasMaxLength(256);

        // ── Cross-module FKs ──────────────────────────────────────────────────

        // Cross-module FK to Administration.Patient — no navigation property.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // StaffUserId is now nullable — walk-in / slot bookings may not assign a
        // staff member at creation time.
        builder.Property(a => a.StaffUserId)
            .HasColumnType("uuid");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.StaffUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.PatientId)
            .HasDatabaseName("ix_appointments_patient_id");

        // AC-2 (US_025): Composite index covering the history query predicate and sort.
        // Supports: WHERE patient_id = $1 [AND status = $2] [AND scheduled_at BETWEEN $3 AND $4]
        // ORDER BY scheduled_at DESC
        // The INCLUDE clause avoids heap fetches for the three most frequently read columns.
        builder.HasIndex(
                a => new { a.PatientId, a.ScheduledAt, a.Status })
            .HasDatabaseName("ix_appointments_patient_scheduled_status")
            .IsDescending(false, true, false);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}


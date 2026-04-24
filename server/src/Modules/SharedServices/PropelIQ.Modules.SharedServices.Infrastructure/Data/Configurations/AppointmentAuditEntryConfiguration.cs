using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AppointmentAuditEntryConfiguration : IEntityTypeConfiguration<AppointmentAuditEntry>
{
    public void Configure(EntityTypeBuilder<AppointmentAuditEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.AppointmentId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.PerformedByUserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.PreviousStatus)
            .HasMaxLength(32);

        builder.Property(e => e.PreviousSlotId)
            .HasColumnType("uuid");

        builder.Property(e => e.NewSlotId)
            .HasColumnType("uuid");

        builder.Property(e => e.PerformedAt)
            .HasDefaultValueSql("now()");

        builder.HasIndex(e => e.AppointmentId)
            .HasDatabaseName("ix_appointment_audit_appointment_id");

        builder.HasIndex(e => e.PerformedAt)
            .HasDatabaseName("ix_appointment_audit_performed_at");
    }
}

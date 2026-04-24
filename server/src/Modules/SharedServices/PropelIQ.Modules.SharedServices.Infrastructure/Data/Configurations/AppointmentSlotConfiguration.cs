using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AppointmentSlotConfiguration : IEntityTypeConfiguration<AppointmentSlot>
{
    public void Configure(EntityTypeBuilder<AppointmentSlot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.ProviderName)
            .HasMaxLength(256);

        builder.Property(s => s.Location)
            .HasMaxLength(256);

        // Duration stored as integer (enum value: 15, 30, 60)
        builder.Property(s => s.Duration)
            .HasConversion<int>();

        builder.Property(s => s.Type)
            .HasConversion<int>();

        // Optimistic concurrency (edge case: booking race condition)
        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        // Composite index for the primary search pattern (AC-1, NFR-002)
        builder.HasIndex(s => new { s.StartTime, s.Type, s.Duration })
            .HasDatabaseName("ix_appointment_slots_starttime_type_duration");

        builder.HasIndex(s => s.ProviderId)
            .HasDatabaseName("ix_appointment_slots_provider_id");

        // Ignore computed property — not persisted to DB
        builder.Ignore(s => s.IsAvailable);
    }
}

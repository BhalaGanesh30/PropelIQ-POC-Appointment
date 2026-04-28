using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// AC-4: Entity configuration for the dead-letter table that stores
/// failed reminder dispatch payloads after retry exhaustion.
/// </summary>
public sealed class DeadLetterEventConfiguration : IEntityTypeConfiguration<DeadLetterEvent>
{
    public void Configure(EntityTypeBuilder<DeadLetterEvent> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.SourceReminderId)
            .IsRequired();

        builder.Property(d => d.AppointmentId)
            .IsRequired();

        builder.Property(d => d.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.FailureReason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.TotalAttempts)
            .IsRequired();

        builder.Property(d => d.Reprocessed)
            .HasDefaultValue(false);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("now()");

        // Index for operational queries: find unprocessed dead-letters.
        builder.HasIndex(d => d.Reprocessed)
            .HasDatabaseName("ix_dead_letter_events_reprocessed")
            .HasFilter("\"Reprocessed\" = false");

        // Index for tracing back to the source reminder.
        builder.HasIndex(d => d.SourceReminderId)
            .HasDatabaseName("ix_dead_letter_events_source_reminder");
    }
}

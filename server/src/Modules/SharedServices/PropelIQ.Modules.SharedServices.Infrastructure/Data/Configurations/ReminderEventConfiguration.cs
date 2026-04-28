using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class ReminderEventConfiguration : IEntityTypeConfiguration<ReminderEvent>
{
    public void Configure(EntityTypeBuilder<ReminderEvent> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.Channel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.SendStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.ConfirmationResponse)
            .HasMaxLength(500);

        // AC-1 / edge case: persist scheduled dispatch time for restart resilience.
        builder.Property(r => r.ScheduledAt)
            .IsRequired();

        // Unique constraint prevents duplicate reminders on retries / duplicate events.
        builder.Property(r => r.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(r => r.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_reminder_events_idempotency_key");

        // Composite index supports the AC-3 bulk-cancel query and dispatch worker queries.
        builder.HasIndex(r => new { r.AppointmentId, r.SendStatus })
            .HasDatabaseName("ix_reminder_events_appointment_send_status");

        // AC-2 / dispatch worker: filtered index on Pending rows ordered by ScheduledAt
        // keeps the due-reminder poll fast even as Sent/Cancelled rows accumulate.
        builder.HasIndex(r => new { r.SendStatus, r.ScheduledAt })
            .HasDatabaseName("ix_reminder_events_pending_scheduled_at")
            .HasFilter("\"SendStatus\" = 'Pending'");

        builder.HasOne(r => r.Appointment)
            .WithMany(a => a.ReminderEvents)
            .HasForeignKey(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

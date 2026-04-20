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

        builder.HasOne(r => r.Appointment)
            .WithMany(a => a.ReminderEvents)
            .HasForeignKey(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

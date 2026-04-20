using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(w => w.Status)
            .IsRequired()
            .HasMaxLength(50);

        // Cross-module FK to Administration.Patient — no navigation property
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(w => w.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Appointment)
            .WithOne(a => a.WaitlistEntry)
            .HasForeignKey<WaitlistEntry>(w => w.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(w => w.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

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

        // Cross-module FK to Administration.Patient — no navigation property
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cross-module FK to Administration.User (staff) — no navigation property
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.StaffUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

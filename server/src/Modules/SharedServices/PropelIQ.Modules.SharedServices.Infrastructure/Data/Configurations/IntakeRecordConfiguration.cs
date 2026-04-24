using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class IntakeRecordConfiguration : IEntityTypeConfiguration<IntakeRecord>
{
    public void Configure(EntityTypeBuilder<IntakeRecord> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        // JSONB columns
        builder.Property(r => r.FormData)
            .HasColumnType("jsonb");

        builder.Property(r => r.AiPopulatedFields)
            .HasColumnType("jsonb");

        // One intake record per appointment (AC-4: prevents duplicate submissions)
        builder.HasIndex(r => r.AppointmentId)
            .IsUnique()
            .HasDatabaseName("ix_intake_records_appointment_id");

        builder.HasIndex(r => r.PatientId)
            .HasDatabaseName("ix_intake_records_patient_id");
    }
}

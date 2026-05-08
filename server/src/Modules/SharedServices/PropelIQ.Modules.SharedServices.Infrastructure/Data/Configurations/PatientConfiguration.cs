using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.MRN)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.MRN)
            .IsUnique();

        builder.OwnsOne(p => p.ContactPreferences, cp =>
        {
            cp.ToJson();
        });

        builder.HasOne(p => p.User)
            .WithOne(u => u.PatientProfile)
            .HasForeignKey<Patient>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("now()");

        // Edge Case 1 (US_033): trigram GIN indexes for fast ILIKE search on
        // patient name and phone in PatientSearchService.  pg_trgm must be
        // enabled (docker/postgres/init/01-create-extensions.sql already does this;
        // the migration also emits CREATE EXTENSION IF NOT EXISTS pg_trgm).
        builder.HasIndex(p => p.FirstName)
            .HasDatabaseName("ix_patients_first_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(p => p.LastName)
            .HasDatabaseName("ix_patients_last_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}

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
    }
}

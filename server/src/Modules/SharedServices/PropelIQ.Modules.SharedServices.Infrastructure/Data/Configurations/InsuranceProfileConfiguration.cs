using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class InsuranceProfileConfiguration : IEntityTypeConfiguration<InsuranceProfile>
{
    public void Configure(EntityTypeBuilder<InsuranceProfile> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.PayerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.MemberId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.VerificationStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(i => i.Patient)
            .WithMany(p => p.InsuranceProfiles)
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(i => i.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

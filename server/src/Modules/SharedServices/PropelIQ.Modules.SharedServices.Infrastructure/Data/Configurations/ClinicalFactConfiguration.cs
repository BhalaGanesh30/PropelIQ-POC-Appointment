using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class ClinicalFactConfiguration : IEntityTypeConfiguration<ClinicalFact>
{
    public void Configure(EntityTypeBuilder<ClinicalFact> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(f => f.FactType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Value)
            .IsRequired();

        builder.Property(f => f.ConfidenceScore)
            .HasPrecision(5, 4);

        builder.Property(f => f.VerificationState)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(f => f.Document)
            .WithMany(d => d.ClinicalFacts)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(f => f.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

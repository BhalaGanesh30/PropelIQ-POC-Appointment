using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
{
    public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.ExtractionStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.StoragePath)
            .HasMaxLength(1000);

        // Cross-module FK to Administration.Patient — no navigation property
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

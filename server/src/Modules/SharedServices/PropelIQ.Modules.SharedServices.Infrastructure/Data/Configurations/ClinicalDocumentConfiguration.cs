using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;

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

        // US_043 AC-2: user-facing display name; falls back to FileName when null.
        builder.Property(d => d.DisplayName)
            .HasMaxLength(255);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty);

        builder.Property(d => d.FileSizeBytes)
            .IsRequired();

        // US_043 AC-1: typed category using document_category_type PostgreSQL enum.
        // The enum type is registered in AppDbContext.OnModelCreating via HasPostgresEnum.
        builder.Property(d => d.Category)
            .HasColumnType("document_category_type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.R2ObjectKey)
            .HasColumnName("r2_object_key")
            .HasMaxLength(512);

        builder.Property(d => d.StoragePath)
            .HasMaxLength(1000);

        builder.Property(d => d.ScanResult)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("PendingScan");

        builder.Property(d => d.ExtractionStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Queued");

        builder.Property(d => d.ExtractedText);

        builder.Property(d => d.NeedsManualReview)
            .IsRequired()
            .HasDefaultValue(false);

        // US_043 AC-3/AC-4: soft-delete fields.
        builder.Property(d => d.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(d => d.DeletedAt);

        // Cross-module FK to Administration.Patient — no navigation property
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("now()");

        // Index for retry service: query by PendingScan (partial equivalent via full index)
        builder.HasIndex(d => d.ScanResult)
            .HasDatabaseName("ix_clinical_documents_scan_result");

        // Index for OCR worker: query by extraction status
        builder.HasIndex(d => d.ExtractionStatus)
            .HasDatabaseName("ix_clinical_documents_extraction_status");

        // US_043: partial index for efficient active-document listing (is_deleted = false).
        builder.HasIndex(d => d.IsDeleted)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_clinical_documents_is_deleted");

        // US_043: composite index for filtered patient document queries.
        builder.HasIndex(d => new { d.PatientId, d.IsDeleted })
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_clinical_documents_patient_active");

        // Database-level 10 MB file size guard (FR-DM-001)
        builder.HasCheckConstraint(
            "chk_clinical_documents_file_size",
            "file_size_bytes <= 10485760");
    }
}


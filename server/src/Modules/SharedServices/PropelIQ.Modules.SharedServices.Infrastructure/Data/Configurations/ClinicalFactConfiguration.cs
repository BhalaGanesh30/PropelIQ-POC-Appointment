using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector.EntityFrameworkCore;
using PropelIQ.Modules.Administration.Domain.Entities;
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

        // AC-2: source document reference for traceability (AIR-004).
        builder.Property(f => f.DocumentId)
            .IsRequired();

        // AIR-010: patient reference for scoped RAG retrieval.
        builder.Property(f => f.PatientId)
            .IsRequired();

        builder.Property(f => f.FactType)
            .IsRequired()
            .HasMaxLength(100);

        // Human-readable entity name (e.g. drug name, allergen). Nullable — older records lack this.
        builder.Property(f => f.Name)
            .HasMaxLength(255);

        builder.Property(f => f.Value)
            .IsRequired();

        builder.Property(f => f.ConfidenceScore)
            .HasPrecision(5, 4);

        // AC-3: low-confidence flag set when ConfidenceScore < ExtractionConfiguration.ConfidenceThreshold.
        builder.Property(f => f.NeedsReview)
            .IsRequired()
            .HasDefaultValue(false);

        // AIR-004: verbatim source text for traceability. Not indexed — used for display only.
        builder.Property(f => f.SourceText);

        // US_044: clinician verification tracking.
        builder.Property(f => f.Verified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(f => f.VerifiedBy);

        // US_047: verification timestamp (AC-1/AC-2, DR-003).
        builder.Property(f => f.VerifiedAt);

        // US_047 Edge Case 1: monotonic row version for atomic optimistic concurrency.
        // Application layer increments via: UPDATE … WHERE row_version = @expected.
        builder.Property(f => f.RowVersion)
            .IsRequired()
            .HasDefaultValue(1)
            .HasColumnName("row_version");

        builder.HasIndex(f => new { f.Id, f.RowVersion })
            .HasDatabaseName("ix_clinical_facts_row_version");
        // Clinical date associated with the fact (e.g. prescription or diagnosis date).
        builder.Property(f => f.FactDate);

        // AIR-010: 1536-dim embedding for patient-scoped RAG retrieval.
        builder.Property(f => f.Embedding)
            .HasColumnType("vector(1536)");

        builder.Property(f => f.VerificationState)
            .IsRequired()
            .HasMaxLength(50);

        // FK: document (cascade) — fact is meaningless without its source document.
        builder.HasOne(f => f.Document)
            .WithMany(d => d.ClinicalFacts)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: patient (restrict) — cross-module reference, no navigation property.
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(f => f.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: verifying user (set null on user deletion).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.VerifiedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(f => f.CreatedAt)
            .HasDefaultValueSql("now()");

        // Index to efficiently fetch all facts for a patient (AIR-010).
        builder.HasIndex(f => f.PatientId)
            .HasDatabaseName("ix_clinical_facts_patient_id");

        // Index to efficiently fetch all facts for a document (AC-2, AIR-004).
        builder.HasIndex(f => f.DocumentId)
            .HasDatabaseName("ix_clinical_facts_document_id");

        // Index to quickly surface facts needing review (AC-3).
        builder.HasIndex(f => f.NeedsReview)
            .HasFilter("needs_review = true")
            .HasDatabaseName("ix_clinical_facts_needs_review");

        // HNSW vector index — created via raw SQL in migration (EF cannot express m/ef_construction params).
        // Registered here so EF is aware of the index metadata (ix_clinical_facts_embedding).
        builder.HasIndex(f => f.Embedding)
            .HasDatabaseName("ix_clinical_facts_embedding")
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // DB-level confidence guard (AC-1).
        builder.HasCheckConstraint(
            "chk_clinical_facts_confidence",
            "confidence_score >= 0.0 AND confidence_score <= 1.0");
    }
}

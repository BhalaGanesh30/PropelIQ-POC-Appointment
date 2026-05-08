using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class CodingDecisionConfiguration : IEntityTypeConfiguration<CodingDecision>
{
    public void Configure(EntityTypeBuilder<CodingDecision> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.CodeType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.SuggestedCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.ConfidenceScore)
            .HasPrecision(5, 4);

        builder.Property(c => c.ReviewerAction)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.FinalizedCode)
            .HasMaxLength(20);

        // Cross-module FK to Administration.Patient — no navigation property
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // US_047 Edge Case 2: nullable FK to clinical fact (restrict — coding decision
        // retains its record when the source fact is soft-deleted or reassigned).
        builder.Property(c => c.FactId);

        builder.HasOne<ClinicalFact>()
            .WithMany()
            .HasForeignKey(c => c.FactId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(c => c.FactId)
            .HasDatabaseName("ix_coding_decisions_fact_id");

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

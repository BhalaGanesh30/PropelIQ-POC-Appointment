using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;

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

        // Map the ReviewerAction enum to the existing VARCHAR(50) column (US_049).
        // HasConversion<string>() keeps the DB column as VARCHAR — avoids an ALTER COLUMN
        // type change that would be destructive on existing rows.
        builder.Property(c => c.ReviewerAction)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(ReviewerAction.Pending);

        builder.Property(c => c.FinalizedCode)
            .HasMaxLength(20);

        // US_049: nullable CPT code reserved for US_050 coding workflow.
        builder.Property(c => c.CptCode)
            .HasMaxLength(20);

        // US_050: timestamp set when clinician accepts/modifies/rejects the suggestion.
        builder.Property(c => c.DecidedAt);

        // US_051/task_003: original AI-suggested code snapshots for AIR-007 agreement rate tracking.
        builder.Property(c => c.OriginalIcd10Code)
            .HasMaxLength(20);

        builder.Property(c => c.OriginalCptCode)
            .HasMaxLength(20);

        // US_055: nullable FK to ai_audit_logs.ai_request_id (composite PK table, no EF nav).
        // Null for manually created decisions that bypass the AI pipeline.
        builder.Property(c => c.AiRequestId)
            .HasColumnType("uuid");

        // US_055: optional clinician note stored alongside the reviewer outcome.
        builder.Property(c => c.ReviewerNote)
            .HasMaxLength(2000);

        // Cross-module FK to Administration.Patient — no navigation property
        builder.HasOne<Patient>()
            .WithMany()
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

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

        // FK to the clinician who reviewed the suggestion (US_050); SET NULL on user deletion
        // so historical decisions are not orphaned when a user account is removed.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Partial index on pending suggestions — accelerates the US_050 pending queue
        // which queries WHERE reviewer_action = 'Pending'.
        builder.HasIndex(c => c.PatientId)
            .HasDatabaseName("ix_coding_decisions_pending")
            .HasFilter("reviewer_action = 'Pending'");

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}

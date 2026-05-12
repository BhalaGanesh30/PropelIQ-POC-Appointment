using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AiAuditLogConfiguration : IEntityTypeConfiguration<AiAuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AiAuditLogEntity> builder)
    {
        // Composite PK required by PostgreSQL range partitioning (PARTITION BY RANGE request_timestamp).
        builder.HasKey(a => new { a.AiRequestId, a.RequestTimestamp });

        builder.Property(a => a.AiRequestId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.RequestTimestamp)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(a => a.ClinicianId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.PromptHash)
            .IsRequired()
            .HasMaxLength(64);

        // Stored as JSONB — use string column; Npgsql serializes directly.
        builder.Property(a => a.ContextRefs)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(a => a.ModelName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ResponsePayload)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(a => a.ConfidenceScores)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(a => a.LatencyMs)
            .IsRequired();

        builder.Property(a => a.FallbackReason)
            .HasMaxLength(255);

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        // B-tree composite index: supports AC-4 admin query by clinician + date range.
        builder.HasIndex(a => new { a.ClinicianId, a.RequestTimestamp })
            .HasDatabaseName("ix_ai_audit_logs_clinician_timestamp")
            .IsDescending(false, true);

        // B-tree index on timestamp alone: supports date-range-only queries and partition pruning.
        builder.HasIndex(a => a.RequestTimestamp)
            .HasDatabaseName("ix_ai_audit_logs_timestamp")
            .IsDescending(true);

        builder.ToTable("ai_audit_logs", "app");
    }
}

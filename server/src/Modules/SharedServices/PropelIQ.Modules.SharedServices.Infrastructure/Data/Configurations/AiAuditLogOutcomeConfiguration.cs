using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AiAuditLogOutcomeConfiguration : IEntityTypeConfiguration<AiAuditLogOutcomeEntity>
{
    public void Configure(EntityTypeBuilder<AiAuditLogOutcomeEntity> builder)
    {
        builder.HasKey(o => o.OutcomeId);

        builder.Property(o => o.OutcomeId)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(o => o.AiRequestId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.ReviewerAction)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.ReviewerNote)
            .HasMaxLength(2000);

        builder.Property(o => o.DecidedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        // B-tree index: supports JOIN from ai_audit_logs admin query (AC-4).
        builder.HasIndex(o => o.AiRequestId)
            .HasDatabaseName("ix_ai_audit_log_outcomes_request_id");

        builder.ToTable("ai_audit_log_outcomes", "app");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

public sealed class AiAuditOutboxConfiguration : IEntityTypeConfiguration<AiAuditOutboxEntity>
{
    public void Configure(EntityTypeBuilder<AiAuditOutboxEntity> builder)
    {
        builder.HasKey(o => o.OutboxId);

        builder.Property(o => o.OutboxId)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(o => o.AiRequestId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(o => o.RetryCount)
            .HasDefaultValue(0);

        builder.Property(o => o.LastAttemptAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(o => o.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        // Partial index for outbox processor: fetch only un-exhausted entries ordered by next due time.
        // WHERE retry_count < 3 filters exhausted records so the processor skips them efficiently.
        builder.HasIndex(o => new { o.RetryCount, o.LastAttemptAt })
            .HasDatabaseName("ix_ai_audit_outbox_retry_due")
            .HasFilter("retry_count < 3");

        builder.ToTable("ai_audit_outbox", "app");
    }
}

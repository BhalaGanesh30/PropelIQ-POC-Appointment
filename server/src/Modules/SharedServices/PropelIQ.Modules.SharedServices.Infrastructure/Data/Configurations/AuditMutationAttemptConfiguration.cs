using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="AuditMutationAttempt"/> (US_056, AC-2).
///
/// Maps to <c>app.audit_mutation_attempts</c>. Provides a queryable record of all
/// rejected UPDATE/DELETE operations on audit tables, complementing pgaudit server logs.
/// </summary>
public sealed class AuditMutationAttemptConfiguration : IEntityTypeConfiguration<AuditMutationAttempt>
{
    public void Configure(EntityTypeBuilder<AuditMutationAttempt> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.AttemptedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Operation)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(a => a.TargetAuditId)
            .HasColumnType("uuid");

        builder.Property(a => a.ErrorMessage)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(a => a.OccurredAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        builder.Property(a => a.SourceIp)
            .HasMaxLength(45);

        builder.HasIndex(a => a.OccurredAt)
            .HasDatabaseName("ix_audit_mutation_attempts_occurred_at")
            .IsDescending(true);

        builder.ToTable("audit_mutation_attempts", "app");
    }
}

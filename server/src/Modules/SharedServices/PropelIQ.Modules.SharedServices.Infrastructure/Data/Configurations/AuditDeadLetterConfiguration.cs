using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="AuditDeadLetter"/> (US_056, AC-2).
///
/// Maps to <c>app.audit_dead_letters</c>. A partial (filtered) index on
/// <c>resolved_at IS NULL</c> ensures the <c>DeadLetterRetryWorker</c> query
/// stays efficient even as the table grows with historically resolved entries.
/// </summary>
public sealed class AuditDeadLetterConfiguration : IEntityTypeConfiguration<AuditDeadLetter>
{
    public void Configure(EntityTypeBuilder<AuditDeadLetter> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(d => d.ErrorMessage)
            .HasColumnType("varchar(2000)")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        builder.Property(d => d.RetryCount)
            .HasDefaultValue(0);

        builder.Property(d => d.LastRetryAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.ResolvedAt)
            .HasColumnType("timestamp with time zone");

        // Filtered index used exclusively by DeadLetterRetryWorker.
        // WHERE resolved_at IS NULL limits the working set to unresolved entries only.
        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("ix_audit_dead_letters_unresolved")
            .HasFilter("resolved_at IS NULL");

        builder.ToTable("audit_dead_letters", "app");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="ComplianceDistributionLog"/> (US_058, AC-3, edge case 2).
///
/// Maps to <c>app.compliance_distribution_log</c>.
/// Append-only — no BaseEntity, no updated_at. Every delivery attempt adds a new row.
/// A composite index on (report_id, attempted_at DESC) supports filtering all attempts
/// for a given report in chronological order (admin failure review, edge case 2).
/// </summary>
public sealed class ComplianceDistributionLogConfiguration : IEntityTypeConfiguration<ComplianceDistributionLog>
{
    public void Configure(EntityTypeBuilder<ComplianceDistributionLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.ReportId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.RecipientId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.RecipientEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(l => l.AttemptedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(l => l.AttemptNumber)
            .IsRequired();

        builder.Property(l => l.ErrorDetail)
            .HasColumnType("text");

        // Composite index: all delivery attempts for a report, newest first.
        builder.HasIndex(l => new { l.ReportId, l.AttemptedAtUtc })
            .HasDatabaseName("ix_compliance_distribution_log_report_attempted")
            .IsDescending(false, true);

        builder.ToTable("compliance_distribution_log", "app");
    }
}

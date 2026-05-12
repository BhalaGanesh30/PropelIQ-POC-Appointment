using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="ComplianceReportRecord"/> (US_058, AC-1, AC-2).
///
/// Maps to <c>app.compliance_reports</c>.
/// Two indexes support the primary access patterns:
/// <list type="bullet">
///   <item>Status + generated_at DESC — report-list endpoint filtering by Completed/Generating.</item>
///   <item>generated_at DESC alone — chronological listing for admin dashboard.</item>
/// </list>
/// <c>pdf_content</c> is stored as <c>bytea</c> — no max length constraint; EF streams it lazily
/// only on download requests (not projected in list queries).
/// </summary>
public sealed class ComplianceReportRecordConfiguration : IEntityTypeConfiguration<ComplianceReportRecord>
{
    public void Configure(EntityTypeBuilder<ComplianceReportRecord> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.ReportType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.PeriodStartUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(r => r.PeriodEndUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(r => r.GeneratedAtUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.TotalAuditEvents)
            .HasDefaultValue(0);

        builder.Property(r => r.UniqueActors)
            .HasDefaultValue(0);

        builder.Property(r => r.AnomalyCount)
            .HasDefaultValue(0);

        builder.Property(r => r.FailedAccessAttempts)
            .HasDefaultValue(0);

        builder.Property(r => r.PdfContent)
            .HasColumnType("bytea");

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Generating");

        builder.Property(r => r.IsAsync)
            .HasDefaultValue(false);

        builder.Property(r => r.JobId)
            .HasColumnType("uuid");

        builder.Property(r => r.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        // Status + date — report-list endpoint filter (AC-2).
        builder.HasIndex(r => new { r.Status, r.GeneratedAtUtc })
            .HasDatabaseName("ix_compliance_reports_status_generated")
            .IsDescending(false, true);

        // Chronological listing — admin dashboard.
        builder.HasIndex(r => r.GeneratedAtUtc)
            .HasDatabaseName("ix_compliance_reports_generated_at")
            .IsDescending(true);

        builder.ToTable("compliance_reports", "app");
    }
}

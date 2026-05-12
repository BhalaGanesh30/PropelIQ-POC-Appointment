using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="ComplianceReportSchedule"/> (US_058, AC-1).
///
/// Maps to <c>app.compliance_report_schedules</c>.
/// A partial index on <c>is_active = TRUE AND next_run_at</c> is used by
/// <c>ComplianceReportScheduleWorker</c> to efficiently find overdue schedules
/// every minute without a full table scan.
/// </summary>
public sealed class ComplianceReportScheduleConfiguration : IEntityTypeConfiguration<ComplianceReportSchedule>
{
    public void Configure(EntityTypeBuilder<ComplianceReportSchedule> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.ReportType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Recurrence)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Monthly");

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);

        builder.Property(s => s.LastRunAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(s => s.NextRunAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        // Partial index used exclusively by the schedule worker (AC-1).
        // Filters to active schedules only — reduces working set for frequent polling.
        builder.HasIndex(s => s.NextRunAt)
            .HasDatabaseName("ix_compliance_report_schedules_next_run")
            .HasFilter("is_active = TRUE");

        builder.ToTable("compliance_report_schedules", "app");
    }
}

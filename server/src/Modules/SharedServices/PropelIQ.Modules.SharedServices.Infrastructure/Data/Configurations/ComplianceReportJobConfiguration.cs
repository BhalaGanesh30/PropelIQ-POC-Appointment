using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="ComplianceReportJob"/> (US_058, edge case 1).
///
/// Maps to <c>app.compliance_report_jobs</c>.
/// A partial index on status IN ('Queued', 'Generating') supports the job worker's
/// recovery query on restart (drain any jobs that were left in-flight if the host crashed).
/// </summary>
public sealed class ComplianceReportJobConfiguration : IEntityTypeConfiguration<ComplianceReportJob>
{
    public void Configure(EntityTypeBuilder<ComplianceReportJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(j => j.ReportId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(j => j.RequestedBy)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(j => j.RequestJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(j => j.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Queued");

        builder.Property(j => j.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(j => j.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(j => j.ErrorMessage)
            .HasColumnType("text");

        // Partial index — job-worker recovery on restart; only in-flight jobs matter.
        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_compliance_report_jobs_inflight")
            .HasFilter("status IN ('Queued', 'Generating')");

        // FK link to the pre-allocated compliance report row (report_id is non-null for jobs).
        builder.HasIndex(j => j.ReportId)
            .HasDatabaseName("ix_compliance_report_jobs_report_id");

        builder.ToTable("compliance_report_jobs", "app");
    }
}

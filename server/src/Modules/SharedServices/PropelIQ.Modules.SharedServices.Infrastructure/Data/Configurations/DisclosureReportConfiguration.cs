using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="DisclosureReport"/> (US_057, AC-2, edge case 1).
///
/// Table: <c>app.disclosure_reports</c>
/// The <c>report_json</c> column is JSONB for efficient storage and integrity.
/// </summary>
public sealed class DisclosureReportConfiguration : IEntityTypeConfiguration<DisclosureReport>
{
    public void Configure(EntityTypeBuilder<DisclosureReport> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.DisclosureRequestId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.ReportJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.AccessEventCount)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.DownloadToken)
            .HasMaxLength(512);

        builder.Property(r => r.DownloadExpiresAt)
            .HasColumnType("timestamp with time zone");

        // The inverse navigation (Request) is configured by DisclosureRequestConfiguration.
        // No duplicate HasOne here to avoid conflicting FK configuration.
        builder.HasIndex(r => r.DisclosureRequestId)
            .IsUnique()
            .HasDatabaseName("ix_disclosure_reports_request_id");
    }
}

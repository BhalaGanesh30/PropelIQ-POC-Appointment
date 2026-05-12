using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="KpiDistributionLog"/> (US_060, AC-4).
///
/// Table: <c>app.kpi_distribution_logs</c>
/// Created by the US_060 task_002 migration.
/// Append-only — rows are never updated after insertion except for
/// <see cref="KpiDistributionLog.Status"/> and <see cref="KpiDistributionLog.ErrorDetail"/>
/// which are set before the initial <c>SaveChangesAsync</c> call.
/// </summary>
public sealed class KpiDistributionLogConfiguration : IEntityTypeConfiguration<KpiDistributionLog>
{
    public void Configure(EntityTypeBuilder<KpiDistributionLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.PeriodFrom)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.PeriodTo)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.RecipientEmails)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.SentAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(l => l.ErrorDetail)
            .HasMaxLength(2000);

        builder.HasIndex(l => new { l.PeriodFrom, l.Status })
            .HasDatabaseName("ix_kpi_distribution_logs_period_status");
    }
}

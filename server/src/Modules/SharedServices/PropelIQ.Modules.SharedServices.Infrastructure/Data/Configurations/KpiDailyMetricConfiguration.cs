using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="KpiDailyMetric"/> (US_060).
///
/// Table: <c>app.kpi_daily_metrics</c>
/// Created by the US_060 task_002 migration.
/// The unique index on <c>date</c> enforces one row per calendar day.
/// </summary>
public sealed class KpiDailyMetricConfiguration : IEntityTypeConfiguration<KpiDailyMetric>
{
    public void Configure(EntityTypeBuilder<KpiDailyMetric> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.Date)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(m => m.NoShowRate)
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(m => m.UtilizationRate)
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(m => m.AverageWaitMinutes)
            .HasColumnType("numeric(8,2)")
            .IsRequired();

        builder.Property(m => m.BookingCount)
            .IsRequired();

        builder.Property(m => m.AvailableSlots)
            .IsRequired();

        builder.Property(m => m.BookedSlots)
            .IsRequired();

        builder.Property(m => m.ComputedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // One row per calendar day — enforced at the DB level.
        builder.HasIndex(m => m.Date)
            .IsUnique()
            .HasDatabaseName("ix_kpi_daily_metrics_date");
    }
}

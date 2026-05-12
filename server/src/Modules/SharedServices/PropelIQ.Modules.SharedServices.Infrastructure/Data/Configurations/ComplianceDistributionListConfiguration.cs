using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table configuration for <see cref="ComplianceDistributionList"/> (US_058, AC-3).
///
/// Maps to <c>app.compliance_distribution_lists</c>.
/// The partial index on <c>is_active = TRUE</c> supports the distributor's query for
/// active recipients without scanning soft-deleted entries.
/// </summary>
public sealed class ComplianceDistributionListConfiguration : IEntityTypeConfiguration<ComplianceDistributionList>
{
    public void Configure(EntityTypeBuilder<ComplianceDistributionList> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.IsActive)
            .HasDefaultValue(true);

        builder.Property(d => d.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        builder.Property(d => d.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");

        // Partial index — distributor queries only active recipients (AC-3).
        builder.HasIndex(d => d.Email)
            .HasDatabaseName("ix_compliance_distribution_lists_active_email")
            .HasFilter("is_active = TRUE");

        builder.ToTable("compliance_distribution_lists", "app");
    }
}

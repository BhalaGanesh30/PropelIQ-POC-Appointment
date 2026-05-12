using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="MergeFieldRegistryEntry"/> (US_062, edge case 2).
///
/// Maps to <c>app.merge_field_registry</c>.
///
/// String primary key on <c>field_name</c> keeps the table self-documenting and avoids
/// a surrogate UUID column that would offer no benefit for a small lookup table.
/// Seed data is managed via the EF migration (see <c>AddMergeFieldRegistry</c>)
/// rather than <c>HasData</c> so that the INSERT statements are visible in plain SQL
/// and easy to audit.
/// </summary>
internal sealed class MergeFieldRegistryConfiguration : IEntityTypeConfiguration<MergeFieldRegistryEntry>
{
    public void Configure(EntityTypeBuilder<MergeFieldRegistryEntry> builder)
    {
        builder.ToTable("merge_field_registry", "app");

        builder.HasKey(e => e.FieldName);

        builder.Property(e => e.FieldName)
            .IsRequired()
            .HasMaxLength(100)
            .ValueGeneratedNever(); // string PK — never auto-generated

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.SampleValue)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("General");

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Index for quick lookup of active merge fields (orphan-detection queries).
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_merge_field_registry_is_active");
    }
}

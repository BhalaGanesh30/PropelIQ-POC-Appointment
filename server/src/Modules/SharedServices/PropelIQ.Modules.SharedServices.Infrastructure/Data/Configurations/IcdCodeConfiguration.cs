using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="IcdCodeEntity"/> → <c>app.icd_codes</c> (US_052, task_003).
///
/// Uses the natural string primary key (e.g. "E11.9"); no auto-generated value.
/// GIN trigram expression index on <c>(code || ' ' || description)</c> is created in the migration
/// for NFR-002 ≤ 500ms p95 similarity search performance (AC-1).
/// Mirrors <c>CptCodeConfiguration</c> structure for UNION query consistency.
/// </summary>
public sealed class IcdCodeConfiguration : IEntityTypeConfiguration<IcdCodeEntity>
{
    public void Configure(EntityTypeBuilder<IcdCodeEntity> builder)
    {
        builder.ToTable("icd_codes", "app");

        // Natural string PK — no auto-generated value.
        builder.HasKey(c => c.Code);
        builder.Property(c => c.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(c => c.Description)
            .IsRequired()
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(c => c.Category)
            .HasColumnName("category")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(c => c.IsDeprecated)
            .IsRequired()
            .HasColumnName("is_deprecated")
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(c => c.EffectiveDate)
            .HasColumnName("effective_date")
            .HasColumnType("date");

        builder.Property(c => c.DeprecationDate)
            .HasColumnName("deprecation_date")
            .HasColumnType("date");

        builder.Property(c => c.LastUpdatedAt)
            .IsRequired()
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // Partial B-tree index: active (non-deprecated) codes — standard point-lookup access pattern.
        builder.HasIndex(c => c.Code)
            .HasDatabaseName("ix_icd_codes_active")
            .HasFilter("is_deprecated = false");

        // B-tree index on last_updated_at DESC — accelerates freshness tracking queries.
        builder.HasIndex(c => c.LastUpdatedAt)
            .HasDatabaseName("ix_icd_codes_last_updated");

        // NOTE: GIN trigram expression index on (code || ' ' || description) is created
        // via raw SQL in the migration (EF Core HasIndex does not support GIN operator classes).
    }
}

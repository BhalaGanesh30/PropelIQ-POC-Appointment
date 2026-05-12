using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="CptCodeEntity"/> → <c>app.cpt_codes</c> (US_050, task_003).
///
/// Uses the natural string primary key; no auto-generated value.
/// </summary>
public sealed class CptCodeConfiguration : IEntityTypeConfiguration<CptCodeEntity>
{
    public void Configure(EntityTypeBuilder<CptCodeEntity> builder)
    {
        builder.ToTable("cpt_codes", "app");

        // Natural string PK — no auto-generated value.
        builder.HasKey(c => c.CptCode);
        builder.Property(c => c.CptCode)
            .HasColumnName("cpt_code")
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

        // Partial index: active codes — most queries filter is_deprecated = false.
        builder.HasIndex(c => c.CptCode)
            .HasDatabaseName("ix_cpt_codes_active")
            .HasFilter("is_deprecated = false");

        // B-tree index on last_updated_at DESC — accelerates MAX(last_updated_at) freshness scan.
        builder.HasIndex(c => c.LastUpdatedAt)
            .HasDatabaseName("ix_cpt_codes_last_updated");
    }
}

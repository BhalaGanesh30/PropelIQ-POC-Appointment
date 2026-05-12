using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core table mapping for <see cref="ConfigurationVersion"/> (US_059, AC-1, AC-3).
///
/// Table: <c>app.configuration_versions</c>
/// <para>
/// <c>values_json</c> and <c>previous_values_json</c> are <c>jsonb</c> columns for
/// efficient storage and potential GIN indexing of configuration diffs.
/// The table is append-only — no UPDATE statements should be issued against it.
/// </para>
/// </summary>
public sealed class ConfigurationVersionConfiguration : IEntityTypeConfiguration<ConfigurationVersion>
{
    public void Configure(EntityTypeBuilder<ConfigurationVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(v => v.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.ValuesJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(v => v.PreviousValuesJson)
            .HasColumnType("jsonb");

        builder.Property(v => v.ChangedByAdminId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(v => v.ChangedByName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.ChangedAtUtc)
            .IsRequired();

        builder.Property(v => v.RestoredFromVersionId)
            .HasColumnType("uuid");

        // Composite index for fast "latest version per category" queries.
        builder.HasIndex(v => new { v.Category, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ix_configuration_versions_category_version");

        // Index to support history queries ordered by category + version descending.
        builder.HasIndex(v => v.Category)
            .HasDatabaseName("ix_configuration_versions_category");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="TemplateVersion"/> (US_062, AC-1, AC-3).
///
/// Maps to <c>app.template_versions</c>.
///
/// Rows in this table are append-only; mutations are forbidden after initial insert.
/// The unique constraint on <c>(template_id, version_number)</c> prevents version-number
/// collisions in concurrent save scenarios.
/// </summary>
internal sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("template_versions", "app");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(v => v.Subject)
            .HasMaxLength(998) // RFC 5321 header field limit
            .IsRequired(false);

        builder.Property(v => v.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(v => v.CreatedByUserId)
            .IsRequired();

        builder.Property(v => v.CreatedByName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(v => v.RestoredFromVersionId)
            .IsRequired(false);

        // FK: template_versions → notification_templates (cascade delete).
        // If a template is hard-deleted all its version rows are removed automatically.
        builder
            .HasOne(v => v.Template)
            .WithMany(t => t.Versions)
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing FK for restore lineage tracking (AC-3).
        // SetNull so deleting a source version does not cascade-delete the restored copy.
        builder
            .HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(v => v.RestoredFromVersionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Uniqueness: one version number per template.
        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber })
            .IsUnique()
            .HasDatabaseName("uq_template_versions_template_version");

        // Efficient history look-ups ordered by newest version first.
        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber })
            .HasDatabaseName("ix_template_versions_template_id_desc");
    }
}

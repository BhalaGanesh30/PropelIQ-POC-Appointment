using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configuration for <see cref="NotificationTemplate"/> (US_062, AC-1).
///
/// Maps to <c>app.notification_templates</c>.
///
/// The self-referencing FK <c>CurrentVersionId</c> → <c>template_versions(id)</c> uses
/// <c>SetNull</c> on delete to avoid cascade cycles with <c>template_versions.template_id</c>
/// which uses <c>Cascade</c>.
/// </summary>
internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates", "app");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Type)
            .IsRequired()
            .HasMaxLength(10); // "HTML" or "SMS"

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(t => t.CurrentVersionId)
            .IsRequired(false);

        // Self-referencing FK to the currently active version.
        // SetNull on delete avoids circular cascade path:
        //   notification_templates → template_versions (cascade on template_id)
        //   template_versions      → notification_templates (set null on current_version_id)
        builder
            .HasOne(t => t.CurrentVersion)
            .WithMany()
            .HasForeignKey(t => t.CurrentVersionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.Type)
            .HasDatabaseName("ix_notification_templates_type");

        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("uq_notification_templates_name");
    }
}

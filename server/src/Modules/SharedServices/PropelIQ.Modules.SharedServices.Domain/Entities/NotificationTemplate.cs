using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Aggregate root for a named notification template (HTML or SMS) (US_062, AC-1).
///
/// <para>
/// Each template has a pointer to its current active <see cref="TemplateVersion"/>.
/// All mutations create a new <see cref="TemplateVersion"/> row — the aggregate root
/// is never updated directly for content changes, preserving the full version history (AC-1, AC-3).
/// </para>
///
/// Maps to <c>app.notification_templates</c> (created by US_062 task_002 migration).
/// </summary>
public sealed class NotificationTemplate : BaseEntity
{
    /// <summary>Human-readable template name (e.g. "Appointment Reminder").</summary>
    public required string Name { get; set; }

    /// <summary>Template channel type: <c>"HTML"</c> or <c>"SMS"</c>.</summary>
    public required string Type { get; set; }

    /// <summary>Optional admin-facing description of the template's purpose.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the currently active <see cref="TemplateVersion"/>.
    /// Null until the first version is saved.
    /// </summary>
    public Guid? CurrentVersionId { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────

    /// <summary>Navigation to the active version (populated when <see cref="CurrentVersionId"/> is set).</summary>
    public TemplateVersion? CurrentVersion { get; set; }

    /// <summary>Full append-only version history for this template.</summary>
    public ICollection<TemplateVersion> Versions { get; set; } = [];
}

using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Immutable snapshot of a notification template's content at a specific point in time (US_062, AC-1, AC-3).
///
/// <para>
/// Rows in this table are never updated after creation. Each edit or restore operation
/// appends a new row with an incremented <see cref="VersionNumber"/>.  Queued
/// notifications reference a specific <see cref="TemplateVersion.Id"/> so they are
/// unaffected by subsequent saves or restores (AC-3).
/// </para>
///
/// Maps to <c>app.template_versions</c> (created by US_062 task_002 migration).
/// </summary>
public sealed class TemplateVersion : BaseEntity
{
    /// <summary>FK to the parent <see cref="NotificationTemplate"/>.</summary>
    public required Guid TemplateId { get; set; }

    /// <summary>Monotonically increasing counter scoped per <see cref="TemplateId"/>.</summary>
    public required int VersionNumber { get; set; }

    /// <summary>Full template body — HTML markup for HTML templates, plain text for SMS.</summary>
    public required string Content { get; set; }

    /// <summary>Email subject line. Required for HTML templates; null for SMS.</summary>
    public string? Subject { get; set; }

    /// <summary>
    /// True when this is the version currently pointed to by <see cref="NotificationTemplate.CurrentVersionId"/>.
    /// Only one version per template may be active at a time.
    /// </summary>
    public required bool IsActive { get; set; }

    /// <summary>UTC timestamp when this version was persisted.</summary>
    public required DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>UUID of the admin who created this version.</summary>
    public required Guid CreatedByUserId { get; set; }

    /// <summary>Display name of the admin (de-normalised for history display — AC-1).</summary>
    public required string CreatedByName { get; set; }

    /// <summary>
    /// When not null, this version was created by a restore operation and contains
    /// the content copied from <see cref="RestoredFromVersionId"/> (AC-3).
    /// </summary>
    public Guid? RestoredFromVersionId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────

    /// <summary>Navigation to the parent template.</summary>
    public NotificationTemplate Template { get; set; } = null!;
}

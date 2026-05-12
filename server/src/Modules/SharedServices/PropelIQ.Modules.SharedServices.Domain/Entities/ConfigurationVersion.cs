using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Append-only snapshot of a configuration category at a point in time (US_059, AC-1, AC-3).
///
/// <para>
/// Every configuration change creates a new row — existing rows are never modified.
/// This models configuration history as an immutable ledger for auditing (AC-3) and
/// rollback (AC-4). The <see cref="VersionNumber"/> auto-increments per <see cref="Category"/>.
/// </para>
///
/// Maps to <c>app.configuration_versions</c> (created by US_059 task_002 migration).
/// </summary>
public sealed class ConfigurationVersion : BaseEntity
{
    /// <summary>Machine-readable category name matching <c>ConfigurationCategory</c> enum values.</summary>
    public required string Category { get; set; }

    /// <summary>Monotonically increasing version counter scoped per <see cref="Category"/>.</summary>
    public required int VersionNumber { get; set; }

    /// <summary>Full JSONB snapshot of all key-value pairs for this category at this version.</summary>
    public required string ValuesJson { get; set; }

    /// <summary>JSONB snapshot of the previous version's values. Null for version 1 of each category.</summary>
    public string? PreviousValuesJson { get; set; }

    /// <summary>UUID of the admin who applied this configuration change.</summary>
    public required Guid ChangedByAdminId { get; set; }

    /// <summary>Display name of the admin at the time of the change (de-normalised for history display, AC-3).</summary>
    public required string ChangedByName { get; set; }

    /// <summary>UTC timestamp when this version was persisted.</summary>
    public required DateTime ChangedAtUtc { get; set; }

    /// <summary>
    /// When this version was created by a rollback operation, references the source version that was restored (AC-4).
    /// Null for normal (non-rollback) updates.
    /// </summary>
    public Guid? RestoredFromVersionId { get; set; }
}

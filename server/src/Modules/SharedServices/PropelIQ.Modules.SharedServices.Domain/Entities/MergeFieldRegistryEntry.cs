namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Canonical merge field definition stored in the database (US_062, edge case 2).
///
/// <para>
/// The <c>merge_field_registry</c> table provides a persistent, auditable record of
/// every supported merge-field token.  Template validation queries this table so that
/// orphaned placeholders — tokens that reference a field that has been deactivated or
/// never existed — can be detected at save time even after an in-process restart.
/// </para>
///
/// <para>
/// This entity uses <c>FieldName</c> as its string primary key to keep SQL FK
/// references readable.  It intentionally does NOT inherit from <see cref="PropelIQ.SharedKernel.BaseEntity"/>
/// because: (1) UUIDs offer no benefit for a small lookup table with a stable natural
/// key, and (2) the absence of <c>CreatedAt</c>/<c>UpdatedAt</c> audit columns
/// simplifies seed-data management.
/// </para>
///
/// Maps to <c>app.merge_field_registry</c>.
/// </summary>
public sealed class MergeFieldRegistryEntry
{
    /// <summary>
    /// Mustache-style token key used inside template content: <c>{{field_name}}</c>.
    /// Serves as the natural primary key.
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>Human-readable label shown to admins in the template editor.</summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Static sample value substituted during preview rendering (AC-2).
    /// Not sensitive — used only to generate non-empty, realistic preview text.
    /// </summary>
    public required string SampleValue { get; set; }

    /// <summary>
    /// Grouping category displayed in the merge-field picker UI
    /// (e.g. "Patient", "Appointment", "Provider", "Organization", "Action").
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// When <c>false</c> the field has been retired.  Templates that still reference
    /// a retired field are flagged with an orphaned-placeholder warning (edge case 2).
    /// Retired entries are never hard-deleted so the audit trail is preserved.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

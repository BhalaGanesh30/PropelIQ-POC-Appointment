namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

/// <summary>
/// ICD-10 diagnosis code catalog entry (US_052, task_003).
///
/// This is a reference table entity — the primary key is the natural ICD-10 code string
/// (e.g. "E11.9"), not an auto-generated UUID. Populated by an external catalog update process;
/// updated via the <c>last_updated_at</c> timestamp.
///
/// Mirrors <see cref="CptCodeEntity"/> structure for UNION query consistency in
/// <c>CodeReferenceRepository</c>.
///
/// Used by:
/// - <c>CodeReferenceRepository</c> — trigram similarity search via pg_trgm.
/// - <c>CodeFavoriteRepository</c>  — join for code descriptions in favorites queries.
/// </summary>
public sealed class IcdCodeEntity
{
    /// <summary>Natural ICD-10 code PK, e.g. "E11.9".</summary>
    public required string Code { get; set; }

    /// <summary>Human-readable description, e.g. "Type 2 diabetes mellitus without complications".</summary>
    public required string Description { get; set; }

    /// <summary>High-level category, e.g. "Endocrine, nutritional and metabolic diseases".</summary>
    public string? Category { get; set; }

    /// <summary>
    /// <c>true</c> when the code has been retired from the active ICD-10 catalog.
    /// Deprecated codes are excluded from search results by default (Edge Case 2).
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>Date on which this code became effective; null for historic entries without date data.</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Date on which this code was deprecated; null while the code is still active.</summary>
    public DateOnly? DeprecationDate { get; set; }

    /// <summary>
    /// UTC timestamp of the last catalog update for this row.
    /// Mirrors <c>CptCodeEntity.LastUpdatedAt</c> for consistent freshness tracking.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; }
}

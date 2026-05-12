namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

/// <summary>
/// CPT (Current Procedural Terminology) code catalog entry (US_050, task_003).
///
/// This is a reference table entity — the primary key is the natural CPT code string
/// (e.g. "99213"), not an auto-generated UUID.  Populated by an external catalog
/// update process; updated via the <c>last_updated_at</c> timestamp.
///
/// Used by:
/// - <c>CptCodeFreshnessService</c>  — queries <c>last_updated_at</c> to detect stale catalogs (Edge Case 2).
/// - <c>CptCodeValidationService</c> — filters LLM-suggested codes to active, non-deprecated entries.
/// </summary>
public sealed class CptCodeEntity
{
    /// <summary>Natural CPT code PK, e.g. "99213".</summary>
    public required string CptCode { get; set; }

    public required string Description { get; set; }

    /// <summary>High-level category, e.g. "E/M Services", "Surgery", "Medicine".</summary>
    public string? Category { get; set; }

    /// <summary>
    /// <c>true</c> when the code has been retired from the active CPT catalog.
    /// Deprecated codes are excluded from all suggestion candidates (Edge Case 2).
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>Date on which this code became effective; null for historic entries without date data.</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Date on which this code was deprecated; null while the code is still active.</summary>
    public DateOnly? DeprecationDate { get; set; }

    /// <summary>
    /// UTC timestamp of the last catalog update for this row.
    /// <c>CptCodeFreshnessService</c> queries <c>MAX(last_updated_at)</c> across the table.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; }
}

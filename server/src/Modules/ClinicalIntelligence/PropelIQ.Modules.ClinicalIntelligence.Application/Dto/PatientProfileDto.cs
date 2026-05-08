namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Full 360° patient profile response (SCR-014, FR-CA-002, US_045 AC-1).
///
/// Facts are grouped by category for direct tab binding on the frontend.
/// <see cref="Timeline"/> is a chronological cross-category view ordered by <c>fact_date</c> descending.
/// When <see cref="Partial"/> is true, at least one category failed; <see cref="PartialSources"/>
/// lists which categories are unavailable (Edge Case 1).
/// </summary>
public sealed record PatientProfileDto
{
    /// <summary>Patient GUID — echoed from the request for client-side correlation.</summary>
    public Guid PatientId { get; init; }

    /// <summary>Medication facts for the patient (paginated).</summary>
    public List<ClinicalFactDto> Medications { get; init; } = [];

    /// <summary>Allergy facts for the patient (paginated).</summary>
    public List<ClinicalFactDto> Allergies { get; init; } = [];

    /// <summary>Diagnosis facts for the patient (paginated).</summary>
    public List<ClinicalFactDto> Diagnoses { get; init; } = [];

    /// <summary>Other clinical findings for the patient (paginated).</summary>
    public List<ClinicalFactDto> Findings { get; init; } = [];

    /// <summary>
    /// Chronological view of all successfully loaded facts ordered by <c>fact_date</c> descending.
    /// Derived from the union of Medications, Allergies, Diagnoses, and Findings.
    /// </summary>
    public List<ClinicalFactDto> Timeline { get; init; } = [];

    /// <summary>
    /// True when at least one fact category query failed (Edge Case 1).
    /// The FE should display a partial data warning banner.
    /// </summary>
    public bool Partial { get; init; }

    /// <summary>
    /// Lists which categories are unavailable along with a sanitized error reason (Edge Case 1).
    /// Empty when <see cref="Partial"/> is false.
    /// </summary>
    public List<PartialSourceDto> PartialSources { get; init; } = [];

    /// <summary>
    /// Total number of facts across all successfully loaded categories.
    /// Used by the FE to decide whether to enable virtual scrolling (Edge Case 2).
    /// </summary>
    public int TotalCount { get; init; }
}

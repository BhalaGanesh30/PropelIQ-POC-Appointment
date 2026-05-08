namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// A single extracted clinical fact with full source traceability (AC-2, AC-3, AIR-004).
/// </summary>
public record ClinicalFactDto
{
    /// <summary>Unique identifier of this clinical fact.</summary>
    public Guid FactId { get; init; }

    /// <summary>Discriminator: medication | allergy | diagnosis | finding.</summary>
    public string FactType { get; init; } = string.Empty;

    /// <summary>Canonical entity name (e.g. drug name, allergen, ICD-10 description).</summary>
    public string? Name { get; init; }

    /// <summary>Full structured value (e.g. dosage, severity, code description).</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>AI extraction confidence score in the range 0.0–1.0 (AC-3, AIR-005).</summary>
    public decimal ConfidenceScore { get; init; }

    /// <summary>
    /// True when <see cref="ConfidenceScore"/> is below the configured threshold (AC-3).
    /// Surfaces a "Low Confidence – Review Required" indicator in the patient profile.
    /// </summary>
    public bool NeedsReview { get; init; }

    /// <summary>True once a clinician has verified this fact.</summary>
    public bool Verified { get; init; }

    /// <summary>Clinical date associated with the fact (e.g. prescription date). Null when unavailable.</summary>
    public DateTimeOffset? FactDate { get; init; }

    /// <summary>
    /// Source traceability: identifies the document from which this fact was extracted (AC-2, AC-3).
    /// Null only when the source document record has been deleted.
    /// </summary>
    public SourceDocumentDto? SourceDocument { get; init; }
}

using Pgvector;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

public sealed class ClinicalFact : BaseEntity
{
    public required Guid DocumentId { get; set; }

    /// <summary>Patient the document belongs to — enables patient-scoped fact queries (AIR-010).</summary>
    public required Guid PatientId { get; set; }

    public required string FactType { get; set; }

    /// <summary>Canonical name of the entity (e.g. drug name, allergen, ICD-10 description).</summary>
    public string? Name { get; set; }

    public required string Value { get; set; }
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// True when <see cref="ConfidenceScore"/> is below the configured threshold (AC-3, AIR-005).
    /// Surfaces a "Low Confidence – Review Required" indicator in the patient profile.
    /// </summary>
    public bool NeedsReview { get; set; }

    /// <summary>Verbatim text segment from which this fact was extracted (AIR-004, AC-2).</summary>
    public string? SourceText { get; set; }

    /// <summary>True once a clinician has verified this fact (US_044 AC-1).</summary>
    public bool Verified { get; set; }

    /// <summary>FK to the user who verified this fact. Null until verified.</summary>
    public Guid? VerifiedBy { get; set; }

    /// <summary>Clinical date/time associated with the fact (e.g. prescription date, diagnosis date).</summary>
    public DateTimeOffset? FactDate { get; set; }

    /// <summary>
    /// 1536-dimension embedding vector for patient-scoped RAG retrieval (AIR-010).
    /// Null until the embedding pipeline processes this fact.
    /// </summary>
    public Vector? Embedding { get; set; }

    public string VerificationState { get; set; } = "Unverified";
    public Guid? LastReviewedByUserId { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }

    /// <summary>Monotonically incrementing version counter for optimistic concurrency (US_047 Edge Case 1).</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Timestamp when the fact was last verified or edited by a clinician (US_047 AC-1/AC-2, DR-003).</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    public ClinicalDocument Document { get; set; } = null!;
}

using PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;
using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

public sealed class CodingDecision : BaseEntity
{
    public required Guid PatientId { get; set; }

    /// <summary>
    /// FK to the source clinical document. Null for manual code selections (US_052, AC-2)
    /// where no document upload was involved.
    /// </summary>
    public Guid? DocumentId { get; set; }

    public required string CodeType { get; set; }
    public required string SuggestedCode { get; set; }
    public string? Rationale { get; set; }
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Lifecycle state of the suggestion (US_049–US_051).
    /// Stored as VARCHAR(50) in the database via EF Core HasConversion.
    /// </summary>
    public ReviewerAction ReviewerAction { get; set; } = ReviewerAction.Pending;

    public string? FinalizedCode { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>Timestamp when the clinician made the accept/modify/reject decision (US_050).</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>
    /// CPT procedure code — nullable; not populated in US_049 (reserved for US_050+).
    /// </summary>
    public string? CptCode { get; set; }

    /// <summary>FK to the clinical fact this decision was derived from. Null for document-level decisions (US_047 Edge Case 2).</summary>
    public Guid? FactId { get; set; }

    /// <summary>
    /// Snapshot of the original AI-suggested ICD-10 code before a Modify action.
    /// Populated only when <see cref="ReviewerAction"/> transitions to <see cref="Domain.Enums.ReviewerAction.Modified"/>.
    /// Used for AIR-007 agreement rate tracking (US_051/task_003).
    /// </summary>
    public string? OriginalIcd10Code { get; set; }

    /// <summary>
    /// Snapshot of the original AI-suggested CPT code before a Modify action.
    /// Populated only when <see cref="ReviewerAction"/> transitions to <see cref="Domain.Enums.ReviewerAction.Modified"/>.
    /// Used for AIR-007 agreement rate tracking (US_051/task_003).
    /// </summary>
    public string? OriginalCptCode { get; set; }

    /// <summary>
    /// Links this decision to the originating AI audit log entry (US_055, AC-2).
    /// Populated by <c>CodingAiGatewayClient</c> when the suggestion is generated.
    /// Null for manual code entries (US_052) that bypass the AI pipeline.
    /// </summary>
    public Guid? AiRequestId { get; set; }

    /// <summary>
    /// Optional clinician note supplied alongside an accept/modify/reject decision.
    /// Stored verbatim; written to <c>ai_audit_log_outcomes</c> for AIR-011 compliance (US_055, AC-2).
    /// </summary>
    public string? ReviewerNote { get; set; }

    public ClinicalDocument? Document { get; set; }
}

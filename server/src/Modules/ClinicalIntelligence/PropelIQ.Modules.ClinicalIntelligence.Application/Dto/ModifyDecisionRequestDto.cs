using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Request payload for the PATCH /api/v1/coding-decisions/{id}/modify endpoint (US_051, AC-2).
///
/// Carries the clinician-supplied final code and description that replace the
/// AI-generated suggestion. The decision ID and reviewer identity are sourced
/// from the route parameter and JWT claims respectively.
/// </summary>
public sealed record ModifyDecisionRequestDto
{
    /// <summary>
    /// The clinician-supplied finalized ICD-10 or CPT code (max 20 characters, required).
    /// Must be a non-empty, valid code string. Business-level code validation is
    /// performed by the workflow service (not this DTO).
    /// </summary>
    [Required]
    [MaxLength(20)]
    public required string FinalCode { get; init; }

    /// <summary>
    /// Human-readable description for the finalized code (required).
    /// Stored in the audit record alongside the original AI-suggested description
    /// for AC-2 audit trail completeness (NFR-010).
    /// </summary>
    [Required]
    public required string FinalDescription { get; init; }

    /// <summary>
    /// Optional clinician note stored verbatim in <c>ai_audit_log_outcomes</c> (US_055, AC-2).
    /// Max 2000 characters. Null if not provided.
    /// </summary>
    [MaxLength(2000)]
    public string? ReviewerNote { get; init; }
}

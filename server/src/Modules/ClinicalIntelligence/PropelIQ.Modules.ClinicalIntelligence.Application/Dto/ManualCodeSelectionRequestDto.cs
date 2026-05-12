using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Request body for the manual code selection endpoint (US_052, AC-2).
///
/// Manual code selection bypasses the AI pipeline and immediately creates a finalized
/// <c>coding_decisions</c> row with <c>reviewer_action = accepted</c>.
/// </summary>
public sealed class ManualCodeSelectionRequestDto
{
    /// <summary>Patient the code is being assigned to.</summary>
    [Required]
    public required Guid PatientId { get; init; }

    /// <summary>The selected code value, e.g. "E11.9" or "99213". Max 20 characters.</summary>
    [Required]
    [MaxLength(20)]
    public required string Code { get; init; }

    /// <summary>Code type discriminator — "icd10" or "cpt".</summary>
    [Required]
    public required string CodeType { get; init; }

    /// <summary>Human-readable description of the selected code.</summary>
    [Required]
    [MaxLength(500)]
    public required string Description { get; init; }
}

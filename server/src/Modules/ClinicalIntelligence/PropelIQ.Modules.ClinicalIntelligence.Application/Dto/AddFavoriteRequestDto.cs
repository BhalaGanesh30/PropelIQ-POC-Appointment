using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Request body for adding a code to the authenticated clinician's favorites (US_052, AC-3).
/// </summary>
public sealed class AddFavoriteRequestDto
{
    /// <summary>The code to favorite, e.g. "E11.9" or "99213". Max 20 characters.</summary>
    [Required]
    [MaxLength(20)]
    public required string Code { get; init; }

    /// <summary>Code type — must be "icd10" or "cpt".</summary>
    [Required]
    [RegularExpression("^(icd10|cpt)$", ErrorMessage = "CodeType must be 'icd10' or 'cpt'.")]
    public required string CodeType { get; init; }
}

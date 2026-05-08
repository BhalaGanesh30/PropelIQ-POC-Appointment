using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Request body for <c>PATCH /api/v1/clinical-facts/{id}</c> (US_047 AC-1).
///
/// At least one of <see cref="Name"/> or <see cref="Value"/> must be supplied;
/// supplying neither would result in a no-op edit, which is rejected (HTTP 400).
/// </summary>
public sealed record PatchFactRequest : IValidatableObject
{
    /// <summary>New canonical name for the fact (max 255 chars). Null = leave unchanged.</summary>
    [MaxLength(255)]
    public string? Name { get; init; }

    /// <summary>New structured value for the fact. Null = leave unchanged.</summary>
    public string? Value { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is null && Value is null)
        {
            yield return new ValidationResult(
                "At least one of 'name' or 'value' must be provided.",
                [nameof(Name), nameof(Value)]);
        }
    }
}

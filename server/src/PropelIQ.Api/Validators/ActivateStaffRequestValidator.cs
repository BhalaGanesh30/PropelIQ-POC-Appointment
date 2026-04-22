using FluentValidation;
using PropelIQ.Api.Models.DTOs;

namespace PropelIQ.Api.Validators;

/// <summary>
/// Validates POST /api/v1/admin/staff/activate payloads.
/// Password rules enforce minimum strength (OWASP A07).
/// </summary>
public sealed class ActivateStaffRequestValidator : AbstractValidator<ActivateStaffRequest>
{
    public ActivateStaffRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation token is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(12).WithMessage("Password must be at least 12 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

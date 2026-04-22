using FluentValidation;
using PropelIQ.Api.Models.DTOs;

namespace PropelIQ.Api.Validators;

/// <summary>
/// Validates POST /api/v1/admin/staff/invite payloads.
/// Role must be one of the allowed staff roles to prevent privilege escalation (OWASP A01).
/// </summary>
public sealed class InviteStaffRequestValidator : AbstractValidator<InviteStaffRequest>
{
    private static readonly string[] AllowedRoles = ["Staff", "Clinician", "Admin"];

    public InviteStaffRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => AllowedRoles.Contains(r, StringComparer.Ordinal))
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}.");
    }
}

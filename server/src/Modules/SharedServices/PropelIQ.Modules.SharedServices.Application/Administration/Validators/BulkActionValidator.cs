using FluentValidation;

namespace PropelIQ.Modules.SharedServices.Application.Administration.Validators;

/// <summary>
/// FluentValidation rules for <see cref="BulkActionRequest"/> (US_061, AC-2, AC-4).
///
/// Validates:
/// <list type="bullet">
///   <item>At least one user ID is supplied.</item>
///   <item>Maximum 200 user IDs per request (performance and safety bound).</item>
///   <item><see cref="BulkActionType"/> value is a defined enum member.</item>
///   <item><see cref="BulkActionRequest.TargetRole"/> is required when action is <see cref="BulkActionType.AssignRole"/>.</item>
/// </list>
/// </summary>
public sealed class BulkActionValidator : AbstractValidator<BulkActionRequest>
{
    /// <summary>Roles assignable through the admin bulk-action interface.</summary>
    private static readonly string[] AssignableRoles = ["Patient", "Staff", "Clinician", "Admin"];

    public BulkActionValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty()
            .WithMessage("At least one user ID must be selected.")
            .Must(ids => ids.Count <= 200)
            .WithMessage("A single bulk action is limited to 200 users. Split the selection and retry.");

        RuleFor(x => x.Action)
            .IsInEnum()
            .WithMessage("Invalid bulk action type. Must be Activate, Deactivate, or AssignRole.");

        When(x => x.Action == BulkActionType.AssignRole, () =>
        {
            RuleFor(x => x.TargetRole)
                .NotEmpty()
                .WithMessage("TargetRole is required when Action is AssignRole.")
                .Must(r => AssignableRoles.Contains(r, StringComparer.Ordinal))
                .WithMessage(
                    $"TargetRole must be one of: {string.Join(", ", AssignableRoles)}.");
        });
    }
}

using FluentValidation;

namespace PropelIQ.Modules.SharedServices.Application.Templates.Validators;

/// <summary>
/// FluentValidation rules for <see cref="SaveTemplateRequest"/> (US_062, AC-4).
///
/// Validates:
/// <list type="bullet">
///   <item><see cref="SaveTemplateRequest.Content"/> is not empty.</item>
///   <item><see cref="SaveTemplateRequest.Content"/> does not exceed 100 KB (safety cap for database).</item>
/// </list>
///
/// Note: merge-field placeholder validation is handled inside
/// <c>TemplateManagementService.ValidateAsync</c> via <see cref="MergeFieldRegistry"/>
/// because it requires access to the template type (HTML vs SMS) and the registry
/// which lives in the Application layer without EF dependencies.
/// </summary>
public sealed class SaveTemplateRequestValidator : AbstractValidator<SaveTemplateRequest>
{
    private const int MaxContentBytes = 100 * 1024; // 100 KB

    public SaveTemplateRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Template content must not be empty.")
            .Must(c => System.Text.Encoding.UTF8.GetByteCount(c) <= MaxContentBytes)
            .WithMessage($"Template content exceeds the maximum allowed size of {MaxContentBytes / 1024} KB.");
    }
}

using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Intake.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Intake.Validators;

/// <summary>
/// Validates the autosave request submitted on each blur event (AC-2).
/// </summary>
public sealed class SaveDraftRequestValidator : AbstractValidator<SaveDraftRequest>
{
    public SaveDraftRequestValidator()
    {
        RuleFor(x => x.FormData)
            .NotNull()
            .WithMessage("Form data is required.");
    }
}

/// <summary>
/// Validates the intake submission request (AC-4).
/// Business-level required-field validation of FormData content
/// is handled by IntakeDraftService after deserializing the JSONB.
/// </summary>
public sealed class IntakeSubmitValidator : AbstractValidator<SubmitIntakeRequest>
{
    public IntakeSubmitValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty()
            .WithMessage("Draft ID is required.");

        RuleFor(x => x.AppointmentId)
            .NotEmpty()
            .WithMessage("Appointment ID is required.");
    }
}

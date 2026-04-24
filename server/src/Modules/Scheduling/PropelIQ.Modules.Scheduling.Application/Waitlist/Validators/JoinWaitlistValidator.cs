using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Waitlist.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Waitlist.Validators;

/// <summary>
/// FluentValidation validator for <see cref="JoinWaitlistRequest"/>.
/// Auto-wired by FluentValidation.AspNetCore before the action runs.
/// </summary>
public sealed class JoinWaitlistValidator : AbstractValidator<JoinWaitlistRequest>
{
    public JoinWaitlistValidator()
    {
        RuleFor(x => x.PreferredDateStart)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Preferred start date must be in the future.");

        RuleFor(x => x.PreferredDateEnd)
            .GreaterThan(x => x.PreferredDateStart)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.PreferredDurationMinutes)
            .Must(d => d is 15 or 30 or 60)
            .WithMessage("Duration must be 15, 30, or 60 minutes.");

        RuleFor(x => x.PreferredAppointmentType)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Appointment type is required and must not exceed 64 characters.");
    }
}

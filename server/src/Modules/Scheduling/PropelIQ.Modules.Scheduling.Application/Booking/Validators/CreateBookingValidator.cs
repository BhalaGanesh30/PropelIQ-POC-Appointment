using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Booking.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Booking.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateBookingRequest"/>.
/// Auto-validated by FluentValidation.AspNetCore before the action runs.
/// </summary>
public sealed class CreateBookingValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.SlotId)
            .NotEmpty()
            .WithMessage("Slot ID is required.");

        RuleFor(x => x.IntakeRecordId)
            .NotEmpty()
            .WithMessage("Intake record ID is required.");
    }
}

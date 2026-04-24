using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Booking.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Booking.Validators;

public sealed class CancelBookingValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingValidator()
    {
        RuleFor(x => x.OverrideReason)
            .MaximumLength(1000)
            .WithMessage("Override reason must not exceed 1000 characters.");
    }
}

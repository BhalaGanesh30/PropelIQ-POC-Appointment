using FluentValidation;
using PropelIQ.Modules.SharedServices.Application.Disclosure;

namespace PropelIQ.Api.Validators;

/// <summary>
/// Validates the payload for POST /api/v1/patients/me/disclosure-requests (US_057, AC-2).
///
/// Business rules enforced:
/// - Both dates are required.
/// - FromDateUtc must be before ToDateUtc.
/// - Date range must not exceed 10 years (async compilation for long ranges — edge case 1).
/// - ToDateUtc must not be in the future (cannot request access logs for future events).
/// </summary>
public sealed class SubmitDisclosureRequestValidator : AbstractValidator<SubmitDisclosureRequest>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(365 * 10);

    public SubmitDisclosureRequestValidator()
    {
        RuleFor(x => x.FromDateUtc)
            .NotEmpty()
            .WithMessage("From date is required.");

        RuleFor(x => x.ToDateUtc)
            .NotEmpty()
            .WithMessage("To date is required.")
            .Must(to => to <= DateTimeOffset.UtcNow)
            .WithMessage("To date must not be in the future.")
            .GreaterThan(x => x.FromDateUtc)
            .WithMessage("To date must be after From date.");

        RuleFor(x => x)
            .Must(x => (x.ToDateUtc - x.FromDateUtc) <= MaxRange)
            .WithMessage("Date range must not exceed 10 years.")
            .OverridePropertyName("DateRange");
    }
}

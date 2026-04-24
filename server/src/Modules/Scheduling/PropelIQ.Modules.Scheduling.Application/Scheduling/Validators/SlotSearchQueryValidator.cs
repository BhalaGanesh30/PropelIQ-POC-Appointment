using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Scheduling;

namespace PropelIQ.Modules.Scheduling.Application.Scheduling.Validators;

/// <summary>
/// Validates slot search query parameters including the 30-day window constraint (AC-4).
/// </summary>
public sealed class SlotSearchQueryValidator : AbstractValidator<SlotSearchQuery>
{
    private const int MaxSearchWindowDays = 30;

    public SlotSearchQueryValidator()
    {
        RuleFor(x => x.DateFrom)
            .GreaterThanOrEqualTo(DateTimeOffset.UtcNow.Date)
            .WithMessage("Start date cannot be in the past.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .WithMessage("End date must be on or after the start date.");

        // AC-4: date range must not exceed 30 days
        RuleFor(x => x)
            .Must(q => (q.DateTo.Date - q.DateFrom.Date).TotalDays <= MaxSearchWindowDays)
            .WithMessage("Slot search is limited to the next 30 days.")
            .WithName("DateRange");

        RuleFor(x => x.DateTo)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.Date.AddDays(MaxSearchWindowDays))
            .WithMessage("Slot search is limited to the next 30 days.");

        RuleFor(x => x.Duration)
            .IsInEnum()
            .When(x => x.Duration.HasValue)
            .WithMessage("Duration must be 15, 30, or 60 minutes.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .When(x => x.Type.HasValue)
            .WithMessage("Invalid appointment type.");
    }
}

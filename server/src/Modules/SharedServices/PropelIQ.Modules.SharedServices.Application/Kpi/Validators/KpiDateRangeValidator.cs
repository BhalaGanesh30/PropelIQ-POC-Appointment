using FluentValidation;

namespace PropelIQ.Modules.SharedServices.Application.Kpi.Validators;

/// <summary>
/// FluentValidation validator for <see cref="DateRange"/> (US_060, AC-2).
///
/// Rules:
/// <list type="bullet">
///   <item><see cref="DateRange.From"/> must be provided.</item>
///   <item><see cref="DateRange.To"/> must be on or after <see cref="DateRange.From"/>.</item>
///   <item>Date span must not exceed 365 days to prevent excessive DB load.</item>
///   <item><see cref="DateRange.To"/> must not be in the future — KPI data is historical.</item>
/// </list>
/// </summary>
public sealed class KpiDateRangeValidator : AbstractValidator<DateRange>
{
    private const int MaxSpanDays = 365;

    public KpiDateRangeValidator()
    {
        RuleFor(r => r.To)
            .GreaterThanOrEqualTo(r => r.From)
            .WithMessage("'To' date must be on or after 'From' date.");

        RuleFor(r => r)
            .Must(r => r.To.DayNumber - r.From.DayNumber + 1 <= MaxSpanDays)
            .WithMessage($"Date range must not exceed {MaxSpanDays} days.")
            .OverridePropertyName("To");

        RuleFor(r => r.To)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("'To' date must not be in the future.");
    }
}

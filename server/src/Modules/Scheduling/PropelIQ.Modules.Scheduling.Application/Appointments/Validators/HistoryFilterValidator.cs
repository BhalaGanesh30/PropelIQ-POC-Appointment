using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Appointments.Dto;

namespace PropelIQ.Modules.Scheduling.Application.Appointments.Validators;

/// <summary>
/// FluentValidation for <see cref="AppointmentHistoryFilter"/>.
/// Auto-wired by FluentValidation.AspNetCore before the action runs — invalid
/// queries receive HTTP 400 with a <c>ValidationProblemDetails</c> body.
///
/// AC-2: status must match a known value.
/// AC-3: date range end must be on or after start.
/// Edge case: page size capped at 100 to prevent unbounded queries.
/// </summary>
public sealed class HistoryFilterValidator : AbstractValidator<AppointmentHistoryFilter>
{
    private static readonly string[] ValidStatuses =
        ["Confirmed", "Completed", "Cancelled", "NoShow", "Rescheduled"];

    public HistoryFilterValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is null || ValidStatuses.Contains(s))
            .WithMessage(
                "Status must be one of: Confirmed, Completed, " +
                "Cancelled, NoShow, Rescheduled.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("End date must be on or after start date.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}

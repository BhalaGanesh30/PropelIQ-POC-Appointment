using FluentValidation;
using PropelIQ.Modules.Scheduling.Application.Reminders.Models;

namespace PropelIQ.Modules.Scheduling.Application.Reminders.Validators;

/// <summary>
/// Validates <see cref="NotificationPreferenceDto"/> on PUT requests.
///
/// Registered automatically via <c>AddValidatorsFromAssemblyContaining</c> in
/// Program.cs (same assembly as <c>SaveDraftRequestValidator</c>).
/// FluentValidation auto-validation returns a 400 before the action body runs
/// when any rule fails — no manual validator invocation required in the controller.
/// </summary>
public sealed class NotificationPreferenceValidator
    : AbstractValidator<NotificationPreferenceDto>
{
    private static readonly HashSet<string> ValidTimings =
        ["7d", "2d", "1d", "2h"];

    public NotificationPreferenceValidator()
    {
        RuleFor(x => x.ReminderTimings)
            .NotNull()
            .WithMessage("ReminderTimings must not be null.")
            .Must(t => t.All(ValidTimings.Contains))
            .WithMessage("Each timing must be one of: 7d, 2d, 1d, 2h.");
    }
}

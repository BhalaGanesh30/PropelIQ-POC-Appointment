using FluentValidation;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Application.Configuration.Validators;

/// <summary>
/// FluentValidation rules for the <c>ReminderRules</c> configuration category (US_059, AC-2).
///
/// <para>Constraints (FR-AD-001):</para>
/// <list type="bullet">
///   <item><c>cadenceHours</c>  — required, integer, ≥ 1.</item>
///   <item><c>maxReminders</c>  — required, integer, 1–10.</item>
/// </list>
/// </summary>
public sealed class ReminderRuleValidator : AbstractValidator<Dictionary<string, object>>
{
    public ReminderRuleValidator()
    {
        RuleFor(v => v)
            .Must(v => v.ContainsKey("cadenceHours"))
            .WithMessage("Reminder cadence is required.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("cadenceHours") ||
                       (TryGetInt(v["cadenceHours"], out var n) && n >= 1))
            .WithMessage("Reminder cadence must be at least 1 hour.");

        RuleFor(v => v)
            .Must(v => v.ContainsKey("maxReminders"))
            .WithMessage("Max reminders is required.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("maxReminders") ||
                       (TryGetInt(v["maxReminders"], out var n) && n >= 1 && n <= 10))
            .WithMessage("Max reminders must be between 1 and 10.");
    }

    private static bool TryGetInt(object? raw, out int value)
    {
        value = 0;
        if (raw is JsonElement je)
            return je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out value);
        return raw is not null && int.TryParse(raw.ToString(), out value);
    }
}

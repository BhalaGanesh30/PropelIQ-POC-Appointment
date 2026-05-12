using FluentValidation;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Application.Configuration.Validators;

/// <summary>
/// FluentValidation rules for the <c>SlotTemplates</c> configuration category (US_059, AC-2).
///
/// <para>Constraints (FR-AD-001):</para>
/// <list type="bullet">
///   <item><c>durationMinutes</c> — required, integer, 5–120.</item>
///   <item><c>bufferMinutes</c>   — optional, integer, 0–30.</item>
/// </list>
/// </summary>
public sealed class SlotTemplateValidator : AbstractValidator<Dictionary<string, object>>
{
    public SlotTemplateValidator()
    {
        RuleFor(v => v)
            .Must(v => v.ContainsKey("durationMinutes"))
            .WithMessage("Slot duration is required.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("durationMinutes") ||
                       (TryGetInt(v["durationMinutes"], out var n) && n >= 5 && n <= 120))
            .WithMessage("Slot duration must be between 5 and 120 minutes.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("bufferMinutes") ||
                       (TryGetInt(v["bufferMinutes"], out var n) && n >= 0 && n <= 30))
            .WithMessage("Buffer time must be between 0 and 30 minutes.");
    }

    private static bool TryGetInt(object? raw, out int value)
    {
        value = 0;
        if (raw is JsonElement je)
            return je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out value);
        return raw is not null && int.TryParse(raw.ToString(), out value);
    }
}

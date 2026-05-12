using FluentValidation;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Application.Configuration.Validators;

/// <summary>
/// FluentValidation rules for the <c>SessionPolicy</c> configuration category (US_059, AC-2).
///
/// <para>Constraints (FR-AD-001):</para>
/// <list type="bullet">
///   <item><c>timeoutMinutes</c>    — required, integer, 5–60.</item>
///   <item><c>warningLeadMinutes</c> — optional, integer, 1–10.</item>
///   <item><c>maxConcurrentSessions</c> — optional, integer, ≥ 1.</item>
/// </list>
/// </summary>
public sealed class SessionPolicyValidator : AbstractValidator<Dictionary<string, object>>
{
    public SessionPolicyValidator()
    {
        RuleFor(v => v)
            .Must(v => v.ContainsKey("timeoutMinutes"))
            .WithMessage("Session timeout is required.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("timeoutMinutes") ||
                       (TryGetInt(v["timeoutMinutes"], out var n) && n >= 5 && n <= 60))
            .WithMessage("Session timeout must be between 5 and 60 minutes.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("warningLeadMinutes") ||
                       (TryGetInt(v["warningLeadMinutes"], out var n) && n >= 1 && n <= 10))
            .WithMessage("Warning lead time must be between 1 and 10 minutes.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("maxConcurrentSessions") ||
                       (TryGetInt(v["maxConcurrentSessions"], out var n) && n >= 1))
            .WithMessage("Max concurrent sessions must be at least 1.");
    }

    private static bool TryGetInt(object? raw, out int value)
    {
        value = 0;
        if (raw is JsonElement je)
            return je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out value);
        return raw is not null && int.TryParse(raw.ToString(), out value);
    }
}

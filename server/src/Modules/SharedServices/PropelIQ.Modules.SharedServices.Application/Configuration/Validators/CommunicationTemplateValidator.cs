using FluentValidation;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Application.Configuration.Validators;

/// <summary>
/// FluentValidation rules for the <c>CommunicationTemplates</c> configuration category (US_059, AC-2).
///
/// <para>Constraints (FR-AD-001):</para>
/// <list type="bullet">
///   <item><c>senderEmail</c> — required, valid RFC 5322 email format.</item>
///   <item><c>footerText</c>  — optional, max 500 characters.</item>
/// </list>
/// </summary>
public sealed class CommunicationTemplateValidator : AbstractValidator<Dictionary<string, object>>
{
    // Basic RFC 5322 compatible email pattern (local@domain.tld).
    private static readonly System.Text.RegularExpressions.Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public CommunicationTemplateValidator()
    {
        RuleFor(v => v)
            .Must(v => v.ContainsKey("senderEmail"))
            .WithMessage("Sender email is required.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("senderEmail") ||
                       (GetString(v["senderEmail"]) is { Length: > 0 } email &&
                        EmailRegex.IsMatch(email)))
            .WithMessage("Sender email must be a valid email address.");

        RuleFor(v => v)
            .Must(v => !v.ContainsKey("footerText") ||
                       (GetString(v["footerText"])?.Length ?? 0) <= 500)
            .WithMessage("Footer text must not exceed 500 characters.");
    }

    private static string? GetString(object? raw) =>
        raw is JsonElement je ? je.GetString() : raw?.ToString();
}

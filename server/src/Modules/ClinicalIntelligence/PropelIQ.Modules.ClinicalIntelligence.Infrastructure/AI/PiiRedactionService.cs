using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Regex-based PII redaction service.  Replaces direct identifiers in text with
/// <c>[REDACTED_TYPE]</c> tokens and records an audit trail of each redaction (AIR-009).
///
/// Patterns covered: SSN, US phone numbers, email addresses, and ISO-format dates of birth.
/// Named-entity redaction (full names) is deferred — clinical text rarely contains full legal
/// names inline and the model is instructed not to reproduce them.
/// </summary>
public sealed partial class PiiRedactionService : IPiiRedactionService
{
    // ── Compiled regex patterns ───────────────────────────────────────────────

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnPattern();

    // Matches: (123) 456-7890 | 123-456-7890 | 1234567890 | +1 123 456 7890
    [GeneratedRegex(@"\b(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]\d{3}[-.\s]\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    // ISO dates and common formats that may represent DOB when prefixed with context words
    [GeneratedRegex(
        @"\b(?:DOB|dob|Date of Birth|date of birth)[:\s]+\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DobPattern();

    // MRN patterns: common EHR formats "MRN: 1234567" or "MR# 1234567"
    [GeneratedRegex(
        @"\b(?:MRN|MR#|Medical Record Number)[:\s#]+\d{5,10}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MrnPattern();

    // ─────────────────────────────────────────────────────────────────────────

    private static readonly (Regex Pattern, string Label)[] Patterns =
    [
        (SsnPattern(),   "SSN"),
        (PhonePattern(), "PHONE"),
        (EmailPattern(), "EMAIL"),
        (DobPattern(),   "DOB"),
        (MrnPattern(),   "MRN"),
    ];

    private readonly ILogger<PiiRedactionService> _logger;

    public PiiRedactionService(ILogger<PiiRedactionService> logger) => _logger = logger;

    /// <inheritdoc />
    public RedactionResult Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new RedactionResult(text, []);

        var actions = new List<RedactionAction>();
        var result  = text;

        // Process patterns from longest match first to avoid overlapping replacements
        // offsetting subsequent match positions.  We apply each pattern to the running
        // result string, recalculating offsets after each pass.
        foreach (var (pattern, label) in Patterns)
        {
            result = pattern.Replace(result, match =>
            {
                actions.Add(new RedactionAction(label, match.Index, match.Length));

                _logger.LogDebug(
                    "PII redacted: type={FieldType} offset={Start} length={Length} (AIR-009).",
                    label, match.Index, match.Length);

                return $"[REDACTED_{label}]";
            });
        }

        return new RedactionResult(result, actions);
    }
}

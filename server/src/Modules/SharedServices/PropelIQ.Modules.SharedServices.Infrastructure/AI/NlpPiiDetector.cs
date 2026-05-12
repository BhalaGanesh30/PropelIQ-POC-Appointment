using System.Text.RegularExpressions;

namespace PropelIQ.Modules.SharedServices.Infrastructure.AI;

/// <summary>
/// A single PII pattern match found in a text segment by <see cref="NlpPiiDetector"/>.
/// </summary>
/// <param name="FieldType">Normalized PII category: SSN | PHONE | DATE | NAME | ADDRESS.</param>
/// <param name="Value">The exact matched text (used to generate the redaction token).</param>
/// <param name="Confidence">Detection confidence 0.0–1.0; exact pattern matches return 1.0.</param>
/// <param name="StartIndex">Zero-based start offset in the analysed text.</param>
/// <param name="Length">Character span of the match.</param>
public sealed record PiiMatch(
    string FieldType,
    string Value,
    double Confidence,
    int StartIndex,
    int Length);

/// <summary>
/// Regex-based NLP entity recogniser for PII patterns in free-text clinical prompts (US_054, Edge Case 2).
///
/// Patterns and confidence assignments:
/// <list type="table">
///   <listheader><term>Field</term><term>Confidence</term><term>Pattern rationale</term></listheader>
///   <item><term>SSN</term><term>1.0</term><term>Exact 9-digit SSN format XXX-XX-XXXX</term></item>
///   <item><term>PHONE</term><term>1.0</term><term>US phone formats including optional +1 prefix</term></item>
///   <item><term>DATE</term><term>0.85</term><term>Short date patterns (may match lab values — below default threshold it won't substitute)</term></item>
///   <item><term>NAME</term><term>0.85</term><term>Capitalized word pair heuristic (may produce false positives for proper nouns)</term></item>
///   <item><term>ADDRESS</term><term>0.90</term><term>Street number + type suffix (strong structural signal)</term></item>
/// </list>
///
/// Patterns below the configured <c>ConfidenceThreshold</c> (default 0.85) are returned
/// but NOT substituted — they are logged as <c>pii_detection_low_confidence</c> events
/// to tune the threshold over time without silently dropping data (Edge Case 2).
/// </summary>
public sealed partial class NlpPiiDetector
{
    // ── Compiled regex patterns (source generators — zero startup allocation) ─

    /// <summary>US Social Security Number: 123-45-6789.</summary>
    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnPattern();

    /// <summary>US phone: (123) 456-7890 | 123-456-7890 | +1 123 456 7890.</summary>
    [GeneratedRegex(@"\b(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]\d{3}[-.\s]\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    /// <summary>Short date formats: 1/1/1990 | 01-01-90 | 2026-05-11 (ISO).</summary>
    [GeneratedRegex(@"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b", RegexOptions.Compiled)]
    private static partial Regex DatePattern();

    /// <summary>Capitalized name pair heuristic: John Doe.</summary>
    [GeneratedRegex(@"\b[A-Z][a-z]{1,29} [A-Z][a-z]{1,29}\b", RegexOptions.Compiled)]
    private static partial Regex NamePattern();

    /// <summary>Street address: 123 Main Street | 45 Oak Ave.</summary>
    [GeneratedRegex(
        @"\b\d{1,5}\s+\w+\s+(Street|St|Avenue|Ave|Road|Rd|Drive|Dr|Boulevard|Blvd|Lane|Ln|Court|Ct)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AddressPattern();

    // ── Pattern registry ──────────────────────────────────────────────────────

    private static readonly (Regex Pattern, string FieldType, double Confidence)[] Patterns =
    [
        (SsnPattern(),     "SSN",     1.0),
        (PhonePattern(),   "PHONE",   1.0),
        (AddressPattern(), "ADDRESS", 0.90),
        (DatePattern(),    "DATE",    0.85),
        (NamePattern(),    "NAME",    0.85),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="text"/> with all registered patterns and returns all matches.
    /// Results are ordered by descending <see cref="PiiMatch.StartIndex"/> so that token
    /// substitution can be applied right-to-left without index drift.
    /// </summary>
    public IReadOnlyList<PiiMatch> Detect(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var matches = new List<PiiMatch>();

        foreach (var (pattern, fieldType, confidence) in Patterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                matches.Add(new PiiMatch(
                    FieldType:  fieldType,
                    Value:      match.Value,
                    Confidence: confidence,
                    StartIndex: match.Index,
                    Length:     match.Length));
            }
        }

        // Descending start-index order ensures right-to-left substitution won't cause index drift.
        matches.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

        return matches;
    }
}

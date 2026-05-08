namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Result of a PII redaction pass on a text segment.
/// Carries the sanitised text and an audit trail of what was redacted (AIR-009).
/// </summary>
/// <param name="RedactedText">Input text with PII tokens replaced by <c>[REDACTED_TYPE]</c> markers.</param>
/// <param name="RedactionActions">Each entry describes one redacted span (type and character range).</param>
public sealed record RedactionResult(
    string RedactedText,
    IReadOnlyList<RedactionAction> RedactionActions);

/// <summary>Describes a single PII redaction event for the audit log.</summary>
/// <param name="FieldType">Type of PII: e.g. <c>SSN</c>, <c>PHONE</c>, <c>EMAIL</c>, <c>DOB</c>.</param>
/// <param name="StartIndex">0-based start character offset in the original text.</param>
/// <param name="Length">Number of characters replaced.</param>
public sealed record RedactionAction(
    string FieldType,
    int StartIndex,
    int Length);

/// <summary>
/// Redacts direct patient identifiers from extracted text before it is submitted
/// to the AI gateway prompt.  Complies with AIR-009 (redact PII; log redaction actions).
/// </summary>
public interface IPiiRedactionService
{
    /// <summary>
    /// Returns a sanitised copy of <paramref name="text"/> with PII tokens replaced,
    /// together with a log of each redaction action for the audit trail.
    /// </summary>
    RedactionResult Redact(string text);
}

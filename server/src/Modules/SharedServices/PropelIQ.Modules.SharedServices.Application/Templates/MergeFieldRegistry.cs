using System.Text.RegularExpressions;

namespace PropelIQ.Modules.SharedServices.Application.Templates;

/// <summary>
/// Canonical registry of allowed merge-field tokens for notification templates (US_062, AC-4, edge cases 1–2).
///
/// <para>
/// Merge fields use the Mustache-style double-brace syntax: <c>{{field_name}}</c>.
/// Only fields registered in this class are permitted in saved templates — unknown
/// placeholders cause a 422 validation error (AC-4).
/// </para>
///
/// <para>
/// Sample values are used by <see cref="Substitute"/> to render preview responses (AC-2).
/// </para>
/// </summary>
public sealed class MergeFieldRegistry
{
    private static readonly IReadOnlyDictionary<string, MergeField> Fields =
        new Dictionary<string, MergeField>(StringComparer.OrdinalIgnoreCase)
        {
            ["patient_name"]       = new("patient_name",       "Patient Name",       "Jane Smith"),
            ["appointment_date"]   = new("appointment_date",   "Appointment Date",   "2026-05-15"),
            ["appointment_time"]   = new("appointment_time",   "Appointment Time",   "10:30 AM"),
            ["clinic_name"]        = new("clinic_name",        "Clinic Name",        "PropelIQ Health Center"),
            ["provider_name"]      = new("provider_name",      "Provider Name",      "Dr. Sarah Johnson"),
            ["appointment_type"]   = new("appointment_type",   "Appointment Type",   "Follow-up Visit"),
            ["cancellation_link"]  = new("cancellation_link",  "Cancellation Link",  "https://propeliq.example.com/cancel/sample"),
            ["reschedule_link"]    = new("reschedule_link",    "Reschedule Link",    "https://propeliq.example.com/reschedule/sample"),
        };

    // Compiled regex — thread-safe and reused across calls.
    private static readonly Regex PlaceholderPattern =
        new(@"\{\{(\w+)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Returns true when <paramref name="fieldName"/> is a registered merge field.</summary>
    public bool IsValid(string fieldName) =>
        Fields.ContainsKey(fieldName);

    /// <summary>Returns all registered merge fields with their display names and sample values.</summary>
    public IReadOnlyDictionary<string, MergeField> GetAll() => Fields;

    /// <summary>
    /// Replaces all <c>{{field_name}}</c> tokens in <paramref name="content"/> with their
    /// sample values. Unrecognised tokens are left unchanged so the caller can detect them
    /// via <see cref="ExtractUnknownPlaceholders"/>.
    /// </summary>
    public string Substitute(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        return PlaceholderPattern.Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            return Fields.TryGetValue(key, out var field)
                ? field.SampleValue
                : match.Value; // leave unknown tokens as-is
        });
    }

    /// <summary>
    /// Extracts all distinct placeholder names from <paramref name="content"/>.
    /// Includes both valid and invalid placeholders.
    /// </summary>
    public List<string> ExtractPlaceholders(string content)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        return PlaceholderPattern
            .Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns placeholder names that appear in <paramref name="content"/> but are
    /// not registered in the field registry (AC-4).
    /// </summary>
    public List<string> ExtractUnknownPlaceholders(string content)
        => ExtractPlaceholders(content)
            .Where(p => !IsValid(p))
            .ToList();
}

/// <summary>Merge field descriptor — name, human-readable label, and preview sample value.</summary>
public sealed record MergeField(
    string Name,
    string DisplayName,
    string SampleValue);

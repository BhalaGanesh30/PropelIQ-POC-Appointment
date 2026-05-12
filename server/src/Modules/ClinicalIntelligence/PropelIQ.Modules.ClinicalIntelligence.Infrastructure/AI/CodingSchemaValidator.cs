using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// JSON schema validator for LLM coding suggestion output (AIR-008).
///
/// Uses <see cref="System.Text.Json"/> with snake_case property names
/// and permissive number handling to tolerate minor model variations.
/// </summary>
internal sealed class CodingSchemaValidator : ICodingSchemaValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling              = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly ILogger<CodingSchemaValidator> _logger;

    public CodingSchemaValidator(ILogger<CodingSchemaValidator> logger) => _logger = logger;

    /// <inheritdoc />
    public LlmCodingResponse? ValidateAndParse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("LLM returned an empty response body.");
            return null;
        }

        LlmCodingResponse? response;
        try
        {
            // Extract the JSON block in case the model prefixes/suffixes prose.
            var jsonSlice = ExtractJsonObject(rawJson);
            response = JsonSerializer.Deserialize<LlmCodingResponse>(jsonSlice, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM response JSON failed to deserialize.");
            return null;
        }

        if (response?.Suggestions is not { Count: > 0 })
        {
            _logger.LogWarning("LLM response contains no suggestions.");
            return null;
        }

        // Validate each suggestion item.
        var valid = response.Suggestions.All(s =>
            !string.IsNullOrWhiteSpace(s.Icd10Code)
            && !string.IsNullOrWhiteSpace(s.Description)
            && s.Confidence is >= 0m and <= 1m);

        if (!valid)
        {
            _logger.LogWarning("One or more LLM suggestion items failed schema validation.");
            return null;
        }

        // Enforce maximum of 3 suggestions (AIR-009).
        if (response.Suggestions.Count > 3)
        {
            response.Suggestions = response.Suggestions
                .OrderByDescending(s => s.Confidence)
                .Take(3)
                .ToList();
        }

        return response;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public LlmCptResponse? ValidateAndParseCpt(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("LLM CPT response is empty.");
            EmitCptMetric(pass: false);
            return null;
        }

        LlmCptResponse? response;
        try
        {
            var jsonSlice = ExtractJsonObject(rawJson);
            response = JsonSerializer.Deserialize<LlmCptResponse>(jsonSlice, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM CPT response failed to deserialize.");
            EmitCptMetric(pass: false);
            return null;
        }

        // Validate CPT suggestions (list may be empty when no CPT mapping exists).
        if (response?.CptSuggestions is null)
        {
            _logger.LogWarning("LLM CPT response is missing cpt_suggestions field.");
            EmitCptMetric(pass: false);
            return null;
        }

        var cptValid = response.CptSuggestions.All(s =>
            !string.IsNullOrWhiteSpace(s.CptCode)
            && !string.IsNullOrWhiteSpace(s.Description)
            && s.Confidence is >= 0m and <= 1m);

        if (!cptValid)
        {
            _logger.LogWarning("One or more LLM CPT suggestion items failed schema validation.");
            EmitCptMetric(pass: false);
            return null;
        }

        // Validate E/M suggestion if present.
        if (response.EmSuggestion is { } em)
        {
            if (string.IsNullOrWhiteSpace(em.EmLevel)
                || string.IsNullOrWhiteSpace(em.Description)
                || em.Confidence is < 0m or > 1m)
            {
                _logger.LogWarning("LLM E/M suggestion failed schema validation.");
                EmitCptMetric(pass: false);
                return null;
            }
        }

        // Enforce maximum of 3 CPT suggestions.
        if (response.CptSuggestions.Count > 3)
        {
            response.CptSuggestions = response.CptSuggestions
                .OrderByDescending(s => s.Confidence)
                .Take(3)
                .ToList();
        }

        EmitCptMetric(pass: true);
        return response;
    }

    private static void EmitCptMetric(bool pass)
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity(pass
                ? "coding.cpt_schema_validation_pass"
                : "coding.cpt_schema_validation_fail");
        activity?.SetTag("schema.pass", pass);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the first <c>{...}</c> block from a raw string to handle cases where
    /// the LLM emits markdown fences or leading prose despite structured output instructions.
    /// </summary>
    private static string ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end   = raw.LastIndexOf('}');

        if (start >= 0 && end >= start)
        {
            return raw[start..(end + 1)];
        }

        return raw;
    }
}

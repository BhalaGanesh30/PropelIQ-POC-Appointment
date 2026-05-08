using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Validates and deserializes the AI gateway response against the clinical extraction
/// schema.  Tracks cumulative pass/fail counts and emits an OTel metric for the
/// rolling pass rate (AC-4, AIR-008).
///
/// Valid JSON must match: <c>{ "facts": [ { fact_type, name, value, confidence, source_text } ] }</c>
/// where <c>fact_type</c> is one of <c>medication | allergy | diagnosis | finding</c>
/// and <c>confidence</c> is a float in [0, 1].
/// </summary>
public sealed class ExtractionSchemaValidator : IExtractionSchemaValidator
{
    private static readonly HashSet<string> ValidFactTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "medication", "allergy", "diagnosis", "finding",
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Histogram<double> PassRateHistogram =
        DiagnosticsConfig.Meter.CreateHistogram<double>(
            "extraction.schema.pass_rate",
            unit: "%",
            description: "Rolling schema-validation pass rate for extraction payloads (AC-4, AIR-008).");

    private int _total;
    private int _passes;

    private readonly ILogger<ExtractionSchemaValidator> _logger;

    public ExtractionSchemaValidator(ILogger<ExtractionSchemaValidator> logger) => _logger = logger;

    /// <inheritdoc />
    public int TotalCount => _total;

    /// <inheritdoc />
    public int PassCount  => _passes;

    /// <inheritdoc />
    public IReadOnlyList<ExtractedFact>? Validate(string rawJson)
    {
        Interlocked.Increment(ref _total);

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            LogFailure("Empty or null AI response.");
            return null;
        }

        ExtractionResponseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ExtractionResponseDto>(rawJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogFailure($"JSON deserialization failed: {ex.Message}");
            return null;
        }

        if (dto?.Facts is null)
        {
            LogFailure("Response missing 'facts' array.");
            return null;
        }

        var facts = new List<ExtractedFact>(dto.Facts.Count);
        foreach (var item in dto.Facts)
        {
            if (string.IsNullOrWhiteSpace(item.FactType) || !ValidFactTypes.Contains(item.FactType))
            {
                LogFailure($"Invalid fact_type: '{item.FactType}'.");
                return null;
            }

            if (item.Confidence is < 0m or > 1m)
            {
                LogFailure($"Confidence {item.Confidence} out of range [0, 1].");
                return null;
            }

            facts.Add(new ExtractedFact(
                FactType:   item.FactType,
                Name:       item.Name ?? string.Empty,
                Value:      item.Value ?? string.Empty,
                Confidence: item.Confidence,
                SourceText: item.SourceText ?? string.Empty));
        }

        Interlocked.Increment(ref _passes);
        EmitPassRate();
        return facts;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void LogFailure(string reason)
    {
        _logger.LogWarning(
            "Extraction schema validation failed: {Reason}. Pass={Pass}/{Total} (AIR-008).",
            reason, _passes, _total);

        EmitPassRate();
    }

    private void EmitPassRate()
    {
        if (_total == 0) return;
        var rate = (_passes / (double)_total) * 100.0;
        PassRateHistogram.Record(rate);

        if (_total >= 10 && rate < 99.0)
        {
            _logger.LogWarning(
                "Extraction schema pass rate {Rate:F1}% is below the 99% target (AC-4, AIR-008). " +
                "Pass={Pass}/{Total}.",
                rate, _passes, _total);
        }
    }

    // ── Internal DTO ─────────────────────────────────────────────────────────

    private sealed class ExtractionResponseDto
    {
        public List<FactItemDto> Facts { get; set; } = [];
    }

    private sealed class FactItemDto
    {
        public string? FactType   { get; set; }
        public string? Name       { get; set; }
        public string? Value      { get; set; }
        public decimal Confidence { get; set; }
        public string? SourceText { get; set; }
    }
}

using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Validates the raw JSON string returned by the AI gateway against the
/// extraction output schema and deserializes it into <see cref="ExtractedFact"/> records.
/// Tracks cumulative pass/fail statistics to enforce the ≥ 99% schema-validation
/// pass rate required by AC-4 and AIR-008.
/// </summary>
public interface IExtractionSchemaValidator
{
    /// <summary>
    /// Attempts to deserialize and validate <paramref name="rawJson"/>.
    /// Returns null when validation fails.
    /// </summary>
    IReadOnlyList<ExtractedFact>? Validate(string rawJson);

    /// <summary>Total validations attempted (for pass-rate telemetry).</summary>
    int TotalCount { get; }

    /// <summary>Validations that passed schema check (for pass-rate telemetry).</summary>
    int PassCount { get; }
}

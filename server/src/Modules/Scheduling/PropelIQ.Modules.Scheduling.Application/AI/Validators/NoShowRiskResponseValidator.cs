using System.Text.Json;
using PropelIQ.Modules.Scheduling.Application.AI.Models;

namespace PropelIQ.Modules.Scheduling.Application.AI.Validators;

/// <summary>
/// Validates and parses the AI gateway's JSON response for no-show risk scoring.
/// AIR-008: Schema validation must succeed for >= 99% of well-formed responses.
/// Returns false on any malformed input so callers fall back to Unknown gracefully.
/// </summary>
public static class NoShowRiskResponseValidator
{
    private static readonly HashSet<string> ValidLevels =
        new(StringComparer.Ordinal) { "Low", "Medium", "High" };

    /// <summary>
    /// Attempts to parse a JSON string into a <see cref="NoShowRiskResult"/>.
    /// Returns <c>false</c> when the response does not conform to the expected schema.
    /// Never throws — all parse failures are handled by the false return path.
    /// </summary>
    public static bool TryParse(string json, out NoShowRiskResult? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // riskLevel must be one of Low, Medium, High
            if (!root.TryGetProperty("riskLevel", out var levelProp))
                return false;

            var riskLevel = levelProp.GetString();
            if (riskLevel is null || !ValidLevels.Contains(riskLevel))
                return false;

            // confidence must be a float in [0.0, 1.0]
            if (!root.TryGetProperty("confidence", out var confidenceProp))
                return false;

            var confidence = confidenceProp.GetDouble();
            if (confidence < 0.0 || confidence > 1.0)
                return false;

            // features must be an array with name + contribution on each element
            if (!root.TryGetProperty("features", out var featuresProp)
                || featuresProp.ValueKind != JsonValueKind.Array)
                return false;

            var features = new List<RiskFeatureContribution>();
            foreach (var f in featuresProp.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var np)
                    ? np.GetString()
                    : null;
                var contribution = f.TryGetProperty("contribution", out var cp)
                    ? cp.GetString()
                    : null;

                if (name is null || contribution is null)
                    return false;

                features.Add(new RiskFeatureContribution(name, contribution));
            }

            result = new NoShowRiskResult(riskLevel, confidence, features);
            return true;
        }
        catch
        {
            // Any unexpected parse error — return false so the caller falls back.
            return false;
        }
    }
}

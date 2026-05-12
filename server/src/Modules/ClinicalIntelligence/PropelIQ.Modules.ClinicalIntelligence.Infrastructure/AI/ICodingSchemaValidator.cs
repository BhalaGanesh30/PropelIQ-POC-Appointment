namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Validates and deserializes the raw JSON string returned by the LLM gateway.
/// Isolates schema-validation concerns so the orchestrator only receives
/// strongly-typed response objects (AIR-008).
/// </summary>
internal interface ICodingSchemaValidator
{
    /// <summary>
    /// Attempts to parse and validate the ICD-10 LLM response JSON.
    /// Returns <c>null</c> when the JSON is malformed or fails schema constraints:
    ///   - Suggestions list must be non-empty.
    ///   - Each item must have a non-empty <c>icd10_code</c> and <c>description</c>.
    ///   - <c>confidence</c> must be in [0, 1].
    /// </summary>
    LlmCodingResponse? ValidateAndParse(string rawJson);

    /// <summary>
    /// Attempts to parse and validate the CPT/E/M LLM response JSON (US_050, AIR-008).
    /// Returns <c>null</c> when the JSON is malformed or fails schema constraints.
    /// Emits <c>coding.cpt_schema_validation_pass</c> / <c>coding.cpt_schema_validation_fail</c>
    /// metrics on completion.
    /// </summary>
    LlmCptResponse? ValidateAndParseCpt(string rawJson);
}


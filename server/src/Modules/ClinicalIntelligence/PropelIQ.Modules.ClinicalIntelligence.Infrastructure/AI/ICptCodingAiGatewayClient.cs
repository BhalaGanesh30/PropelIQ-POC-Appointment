using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// AI gateway client scoped to the CPT/E/M coding suggestion pipeline (US_050).
///
/// Assembles a CPT-specific prompt with appointment type context, clinical evidence,
/// and structured JSON output schema requirements.
/// </summary>
internal interface ICptCodingAiGatewayClient
{
    /// <summary>
    /// Sends a CPT/E/M suggestion request to the LLM.
    /// Returns the raw JSON response string, or <c>null</c> when the circuit breaker
    /// is open or an unrecoverable error occurs.
    /// </summary>
    Task<string?> RequestCptSuggestionsAsync(
        string appointmentType,
        IReadOnlyList<EvidenceChunk> evidence,
        CancellationToken ct = default);
}

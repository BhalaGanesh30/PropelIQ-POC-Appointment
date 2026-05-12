using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// AI gateway client scoped to the ICD-10 coding suggestion pipeline (US_049, US_054).
///
/// Encapsulates:
///   - PII redaction of the assembled prompt before dispatch (US_054, AC-1).
///   - ACL filter on evidence context chunks (US_054, AC-4, AIR-010).
///   - Prompt assembly from evidence chunks.
///   - Structured JSON output schema enforcement.
///   - De-anonymization of the LLM response (US_054, AC-3).
///   - Polly circuit-breaker resilience (delegated to the underlying HttpClient handler — AIR-006).
/// </summary>
internal interface ICodingAiGatewayClient
{
    /// <summary>
    /// Sends a coding suggestion request to the LLM and returns the raw JSON response string.
    /// Applies PII redaction before sending and de-anonymization after receiving the response.
    /// Returns null when the circuit breaker is open, an unrecoverable gateway error occurs, or
    /// the PII pipeline fails — triggering the manual fallback path (AIR-005, US_054 Edge Case 1).
    /// </summary>
    /// <param name="evidence">Evidence chunks retrieved from the pgvector HNSW index.</param>
    /// <param name="patientId">Patient scope — used in the redaction context and ACL filter (AC-4).</param>
    /// <param name="clinicianId">Requesting clinician — written to PII audit events (AC-2).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> RequestSuggestionsAsync(
        IReadOnlyList<EvidenceChunk> evidence,
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default);
}

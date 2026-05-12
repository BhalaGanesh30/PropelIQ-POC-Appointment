namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// Thrown when the PII redaction pipeline fails before the prompt reaches the AI model.
///
/// Any exception in Phase 1 (structured field redaction, NLP detection, Redis store,
/// ACL filter) is caught, wrapped in this exception, and re-thrown so callers can
/// return a safe HTTP 500 / AI fallback response without leaking raw PII (Edge Case 1, US_054).
///
/// Callers (e.g., <c>CodingAiGatewayClient</c>) should catch this and return a fallback
/// result rather than propagating the inner exception, to prevent partial-redacted prompts
/// from reaching the LiteLLM gateway.
/// </summary>
public sealed class PiiRedactionFailureException : Exception
{
    public PiiRedactionFailureException(string message)
        : base(message) { }

    public PiiRedactionFailureException(string message, Exception innerException)
        : base(message, innerException) { }
}

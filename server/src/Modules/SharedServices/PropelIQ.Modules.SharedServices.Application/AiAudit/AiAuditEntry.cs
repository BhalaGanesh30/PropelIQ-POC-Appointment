namespace PropelIQ.Modules.SharedServices.Application.AiAudit;

/// <summary>
/// Payload passed to <see cref="IAiAuditService.LogAiRequestAsync"/> after each AI gateway call.
///
/// All fields are mandatory; omit none to preserve immutable provenance (AIR-011, AC-1).
/// </summary>
/// <param name="AiRequestId">
/// Correlation ID from <c>RedactionContext.CorrelationId</c>.  Used to join
/// <c>ai_audit_logs</c> with <c>ai_audit_log_outcomes</c> (AC-2).
/// </param>
/// <param name="RequestTimestamp">UTC timestamp when the AI request was dispatched.</param>
/// <param name="ClinicianId">User ID of the requesting clinician.</param>
/// <param name="PromptHash">SHA-256 hex digest of the redacted prompt (never raw PII).</param>
/// <param name="ContextRefs">JSON-serialized array of context chunk references included in the prompt.</param>
/// <param name="ModelName">Model alias as configured in LiteLLM (e.g., <c>"coding-suggestion"</c>).</param>
/// <param name="ResponsePayload">De-anonymized LLM response as a raw JSON string.</param>
/// <param name="ConfidenceScores">JSON-serialized model confidence scores per code suggestion.</param>
/// <param name="LatencyMs">End-to-end latency from request dispatch to response receipt.</param>
/// <param name="FallbackReason">Populated when the gateway returned a fallback response; null otherwise.</param>
public sealed record AiAuditEntry(
    Guid            AiRequestId,
    DateTimeOffset  RequestTimestamp,
    Guid            ClinicianId,
    string          PromptHash,
    string          ContextRefs,
    string          ModelName,
    string          ResponsePayload,
    string          ConfidenceScores,
    int             LatencyMs,
    string?         FallbackReason);

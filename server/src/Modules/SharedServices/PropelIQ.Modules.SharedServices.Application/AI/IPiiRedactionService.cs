namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// PII redaction and de-anonymization pipeline for AI prompts (US_054, AIR-009).
///
/// Phase 1 (pre-prompt): <see cref="RedactAsync"/> strips direct identifiers from the
/// prompt text and replaces them with deterministic HMAC-derived tokens. The token map
/// is encrypted and stored in Redis with a 5-minute TTL.
///
/// Phase 2 (post-response): <see cref="DeAnonymizeAsync"/> retrieves the token map and
/// restores the original values in the LLM response text, then deletes the Redis entry.
///
/// On any pipeline failure, a <see cref="PiiRedactionFailureException"/> is thrown so
/// callers can return a safe fallback — no raw PII ever reaches the model (Edge Case 1).
/// </summary>
public interface IPiiRedactionService
{
    /// <summary>
    /// Redacts PII from <paramref name="prompt"/> and returns the sanitised prompt with a
    /// <see cref="RedactionContext"/> containing the correlation ID and token map.
    ///
    /// Steps performed:
    /// <list type="number">
    ///   <item>Structured field scan (patient_name, dob, ssn, address, phone patterns).</item>
    ///   <item>NLP free-text scan via <c>NlpPiiDetector</c> above the configured confidence threshold.</item>
    ///   <item>Token map persisted to encrypted Redis (5-minute TTL).</item>
    ///   <item><c>pii_redacted</c> audit event written (AC-2, NFR-010).</item>
    /// </list>
    ///
    /// On any failure: <c>pii_redaction_failed</c> audit event is written and
    /// <see cref="PiiRedactionFailureException"/> is thrown (Edge Case 1).
    /// </summary>
    /// <param name="prompt">Raw prompt text that may contain PII.</param>
    /// <param name="patientId">Patient scope for the AI request (used in ACL and audit log).</param>
    /// <param name="clinicianId">Authenticated clinician ID (audit actor, AC-2).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Sanitised prompt text and the <see cref="RedactionContext"/> for Phase 2.</returns>
    /// <exception cref="PiiRedactionFailureException">Thrown when the pipeline fails (Edge Case 1).</exception>
    Task<(string RedactedPrompt, RedactionContext Context)> RedactAsync(
        string prompt,
        Guid patientId,
        Guid clinicianId,
        CancellationToken ct = default);

    /// <summary>
    /// Restores anonymization tokens in <paramref name="responseText"/> using the redaction map
    /// stored in Redis under <paramref name="correlationId"/>.
    ///
    /// Steps performed:
    /// <list type="number">
    ///   <item>Retrieve and decrypt token map from Redis.</item>
    ///   <item>Replace each <c>[REDACTED_*]</c> token with its original value.</item>
    ///   <item>Delete the Redis key (cleanup — TTL is the safety net).</item>
    ///   <item><c>pii_deanonymized</c> audit event written with token count only — no raw values (AC-2, AC-3).</item>
    /// </list>
    ///
    /// Returns <paramref name="responseText"/> unchanged when no map is found (graceful miss).
    /// </summary>
    /// <param name="responseText">Raw LLM response text that may contain redaction tokens.</param>
    /// <param name="correlationId">Correlation ID from the preceding <see cref="RedactAsync"/> call.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>De-anonymized response text with original context values restored (AC-3).</returns>
    Task<string> DeAnonymizeAsync(
        string responseText,
        Guid correlationId,
        CancellationToken ct = default);
}

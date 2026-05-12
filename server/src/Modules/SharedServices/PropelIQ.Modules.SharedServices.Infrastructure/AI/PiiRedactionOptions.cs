namespace PropelIQ.Modules.SharedServices.Infrastructure.AI;

/// <summary>
/// Configuration POCO for the PII redaction pipeline (US_054).
/// Bound from <c>IConfiguration.GetSection("AI:Redaction")</c>.
///
/// Keys consumed:
/// <list type="bullet">
///   <item><c>AI:Redaction:ConfidenceThreshold</c> — minimum NLP pattern confidence to trigger substitution (Edge Case 2).</item>
///   <item><c>AI:Redaction:HmacKey</c> — Base64-encoded 32-byte HMAC-SHA256 key for deterministic token generation (AC-1, AC-3).</item>
///   <item><c>AI:Redaction:EncryptionKey</c> — Base64-encoded 32-byte AES-256-GCM key for Redis token-map encryption (AC-3).</item>
///   <item><c>AI:Redaction:StructuredFields</c> — comma-separated field names to scan for direct identifier patterns.</item>
///   <item><c>AI:Redaction:MaxRedactionTimeMs</c> — pipeline timeout guard in milliseconds (default 500).</item>
/// </list>
///
/// In production, <c>HmacKey</c> and <c>EncryptionKey</c> MUST be sourced from a secrets vault
/// (Azure Key Vault / environment variable injection) — never from appsettings.json in plain text.
/// </summary>
public sealed class PiiRedactionOptions
{
    public const string SectionName = "AI:Redaction";

    /// <summary>
    /// Minimum confidence score (0.0–1.0) for an NLP pattern match to trigger token substitution.
    /// Matches below this threshold are logged as <c>pii_detection_low_confidence</c> but not replaced
    /// (Edge Case 2). Default: 0.85.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.85;

    /// <summary>
    /// Base64-encoded 32-byte HMAC-SHA256 key used to derive deterministic redaction tokens.
    /// A stable key ensures the same PII value always produces the same token within a tenant,
    /// enabling de-anonymization via the stored token map (AC-1, AC-3).
    /// Empty string triggers a dev-only fallback — MUST be configured in production.
    /// </summary>
    public string HmacKey { get; init; } = string.Empty;

    /// <summary>
    /// Base64-encoded 32-byte AES-256 key used to encrypt the token map stored in Redis (AC-3).
    /// Empty string triggers a dev-only fallback — MUST be configured in production.
    /// </summary>
    public string EncryptionKey { get; init; } = string.Empty;

    /// <summary>
    /// Structured field names scanned for direct identifier patterns in the prompt.
    /// Each name is matched using the pattern <c>field_name\s*:\s*"?value"?</c>.
    /// Default covers the five direct identifiers from AIR-009.
    /// </summary>
    public string[] StructuredFields { get; init; } =
        ["patient_name", "date_of_birth", "ssn", "address", "phone"];

    /// <summary>
    /// Maximum time in milliseconds the full redaction pipeline may run before the call
    /// is considered timed-out and <see cref="Application.AI.PiiRedactionFailureException"/> is thrown.
    /// Default: 500 ms.
    /// </summary>
    public int MaxRedactionTimeMs { get; init; } = 500;
}

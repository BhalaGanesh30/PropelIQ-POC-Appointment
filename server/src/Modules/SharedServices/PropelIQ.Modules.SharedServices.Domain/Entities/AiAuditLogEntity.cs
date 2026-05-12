namespace PropelIQ.Modules.SharedServices.Domain.Entities;

/// <summary>
/// Append-only entity representing a single AI gateway request audit record (US_055, AIR-011).
///
/// Persisted in <c>app.ai_audit_logs</c>, a PostgreSQL range-partitioned table (by year).
/// No UPDATE or DELETE is permitted at the DB role level (AC-3, DR-005, NFR-010).
///
/// Primary key is composite (<c>AiRequestId</c>, <c>RequestTimestamp</c>) — required by
/// PostgreSQL's range partitioning constraint.
/// </summary>
public sealed class AiAuditLogEntity
{
    /// <summary>
    /// Caller-supplied correlation ID (same as <c>CorrelationId</c> from <c>RedactionContext</c>).
    /// Forms the first part of the composite PK.
    /// </summary>
    public required Guid AiRequestId { get; init; }

    /// <summary>UTC timestamp of the AI request. Forms the second part of the composite PK for partition routing.</summary>
    public required DateTimeOffset RequestTimestamp { get; init; }

    /// <summary>Clinician who initiated the AI request.</summary>
    public required Guid ClinicianId { get; init; }

    /// <summary>
    /// SHA-256 hex digest of the redacted prompt — never raw PII (AIR-009, AIR-011).
    /// 64 hex characters = 256 bits.
    /// </summary>
    public required string PromptHash { get; init; }

    /// <summary>
    /// JSON array of context document/chunk references included in the prompt (AIR-011).
    /// Format: <c>[{ "factId": "...", "factType": "...", "distance": 0.12 }]</c>.
    /// Stored as JSONB in Postgres for indexed querying.
    /// </summary>
    public required string ContextRefs { get; init; }

    /// <summary>Model alias used for the request (e.g., <c>"coding-suggestion"</c>).</summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Full de-anonymized LLM response stored as JSONB for provenance (AIR-011).
    /// The response is de-anonymized by the PII pipeline before storage — no raw tokens appear.
    /// </summary>
    public required string ResponsePayload { get; init; }

    /// <summary>
    /// Model-reported confidence scores per code suggestion (AIR-011).
    /// Format: <c>{ "ICD-10": 0.91, "CPT": 0.88 }</c>.
    /// </summary>
    public required string ConfidenceScores { get; init; }

    /// <summary>End-to-end latency from request dispatch to response receipt (milliseconds).</summary>
    public required int LatencyMs { get; init; }

    /// <summary>Populated when the AI gateway returned a fallback response (AC-1).</summary>
    public string? FallbackReason { get; init; }

    /// <summary>Row creation timestamp (mirrors <see cref="RequestTimestamp"/> in most cases).</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

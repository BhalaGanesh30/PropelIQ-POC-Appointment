namespace PropelIQ.Modules.SharedServices.Application.AI;

/// <summary>
/// Carries per-request redaction metadata across the PII pipeline phases (US_054, AC-1 through AC-3).
///
/// A new instance is created by <see cref="IPiiRedactionService.RedactAsync"/> for each
/// AI request and flows through to <see cref="IPiiRedactionService.DeAnonymizeAsync"/>.
///
/// <see cref="TokenMap"/> is populated during <c>RedactAsync</c> and consumed during
/// <c>DeAnonymizeAsync</c> to restore original values without re-exposing raw PII in logs.
/// The map is persisted in Redis (encrypted, 5-minute TTL) under key
/// <c>redaction:{CorrelationId}</c> and deleted after successful de-anonymization.
/// </summary>
public sealed record RedactionContext
{
    /// <summary>Unique ID for this redaction round-trip; links Phase 1 and Phase 2 audit events.</summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>Patient scope — ensures the redaction map is keyed to the correct patient context.</summary>
    public required Guid PatientId { get; init; }

    /// <summary>Clinician making the request — written to audit log as the acting user (AC-2).</summary>
    public required Guid ClinicianId { get; init; }

    /// <summary>
    /// Maps each anonymization token to its original plaintext value.
    /// Keys: <c>[REDACTED_FIELDTYPE_hash]</c> tokens substituted in the prompt.
    /// Values: original PII values (never logged — stored only in encrypted Redis).
    /// Built incrementally during <c>RedactAsync</c>.
    /// </summary>
    public Dictionary<string, string> TokenMap { get; } = new(StringComparer.Ordinal);

    /// <summary>Timestamp of redaction context creation.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

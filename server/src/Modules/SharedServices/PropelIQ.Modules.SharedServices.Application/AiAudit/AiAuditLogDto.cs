namespace PropelIQ.Modules.SharedServices.Application.AiAudit;

/// <summary>
/// Query DTO returned by <see cref="IAiAuditService.QueryAsync"/> for the admin endpoint (AC-4).
///
/// Includes core request fields only — <c>ResponsePayload</c> and <c>ContextRefs</c> are
/// intentionally omitted from the list view to keep payload size bounded.
/// The full record can be retrieved individually if a detail endpoint is added later.
/// </summary>
public sealed record AiAuditLogDto(
    Guid            AiRequestId,
    DateTimeOffset  RequestTimestamp,
    Guid            ClinicianId,
    string          PromptHash,
    string          ModelName,
    int             LatencyMs,
    string?         FallbackReason,
    DateTimeOffset  CreatedAt);

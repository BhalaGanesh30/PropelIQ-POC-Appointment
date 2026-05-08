namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Cross-cutting audit service for creating and querying immutable audit records
/// (NFR-010: 7-year retention, append-only per DR-005).
///
/// All write methods are append-only — no AuditRecord is ever updated or deleted.
/// Callers must persist their own domain changes in the same transaction; the
/// service adds the audit row to the current EF Core change-tracker and relies on
/// the caller's <c>SaveChangesAsync</c> (or its own internal transaction) for atomicity.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records a scheduling constraint override event with full forensic detail (AC-2).
    ///
    /// Writes an <c>AuditRecord</c> with <c>EventType = "Override"</c>, the staff
    /// member's identity, the affected appointment, and a Metadata dictionary containing
    /// <c>constraintType</c>, <c>reason</c>, <c>action</c>, and <c>overrideRecordId</c>.
    ///
    /// Called inside the caller's transaction so that the audit write and the
    /// scheduling action are atomic per DR-002.
    /// </summary>
    /// <param name="payload">Structured override event data.</param>
    /// <param name="staffUserId">UUID of the authenticated staff member.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>UUID of the created AuditRecord.</returns>
    Task<Guid> LogOverrideAsync(
        OverrideAuditPayload payload,
        Guid staffUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Records a staff-assisted booking event with on-behalf-of attribution (AC-4, NFR-010).
    ///
    /// Writes an <c>AuditRecord</c> with <c>EventType = "StaffBooking"</c>, the staff
    /// member's identity, the created appointment, and Metadata containing visit reason,
    /// optional override reason, and inline patient creation flag.
    ///
    /// Added to the EF change tracker — caller must call <c>SaveChangesAsync</c> or
    /// this method persists within its own <c>SaveChangesAsync</c> call.
    /// </summary>
    /// <param name="payload">Structured staff booking event data.</param>
    /// <param name="staffUserId">UUID of the authenticated staff member.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>UUID of the created AuditRecord.</returns>
    Task<Guid> LogStaffBookingAsync(
        StaffBookingAuditPayload payload,
        Guid staffUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns paginated audit records filtered by optional criteria (AC-4).
    /// </summary>
    /// <param name="eventType">
    /// Optional event type filter (e.g., <c>"Override"</c>).
    /// Pass <c>null</c> to return all event types.
    /// </param>
    /// <param name="from">Optional inclusive start of the time range (UTC).</param>
    /// <param name="to">Optional inclusive end of the time range (UTC).</param>
    /// <param name="pageSize">Number of records per page (default 50, max 200).</param>
    /// <param name="page">0-based page index.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<AuditEntryDto>> GetAuditEntriesAsync(
        string? eventType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageSize = 50,
        int page = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Records a generic domain event with caller-supplied metadata (NFR-010, DR-005).
    ///
    /// Designed for cross-cutting events that do not fit the specialized override or
    /// booking payloads — e.g., <c>conflict_acknowledged</c> in the ClinicalIntelligence module.
    ///
    /// Adds the <c>AuditRecord</c> to the EF change tracker and calls
    /// <c>SaveChangesAsync</c> within its own transaction.  Callers that require
    /// atomicity with their own writes should call this method after their own save.
    /// </summary>
    /// <param name="eventType">Machine-readable event identifier (e.g. "conflict_acknowledged").</param>
    /// <param name="actorUserId">UUID of the authenticated user performing the action.</param>
    /// <param name="targetEntityId">UUID of the entity being acted upon.</param>
    /// <param name="targetEntityType">Type name of the target entity (e.g. "conflict_alert").</param>
    /// <param name="metadata">Arbitrary key-value pairs for forensic detail (max 20 entries).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>UUID of the created AuditRecord.</returns>
    Task<Guid> LogEventAsync(
        string eventType,
        Guid actorUserId,
        Guid? targetEntityId,
        string targetEntityType,
        Dictionary<string, string> metadata,
        CancellationToken ct = default);
}

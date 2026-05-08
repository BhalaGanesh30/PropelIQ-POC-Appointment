namespace PropelIQ.Modules.Scheduling.Application.Override.Dto;

/// <summary>
/// Response body from <c>POST /api/v1/scheduling/override</c> (EP-004 US_034 AC-2).
///
/// Returns both the override record ID and the audit record ID so the frontend
/// can correlate the operation and surface it in the audit log (AC-4).
/// </summary>
public sealed class SchedulingOverrideResponse
{
    /// <summary>UUID of the created override record.</summary>
    public Guid OverrideId { get; init; }

    /// <summary>
    /// UUID of the immutable <c>AuditRecord</c> written as part of the override transaction.
    /// Correlates with <c>GET /api/v1/audit?actionType=Override</c> (AC-4, NFR-010).
    /// </summary>
    public Guid AuditRecordId { get; init; }

    /// <summary>Human-readable outcome (always <c>"Applied"</c> on success).</summary>
    public string Status { get; init; } = "Applied";

    /// <summary>UUID of the affected appointment — mirrors the request for client convenience.</summary>
    public Guid AppointmentId { get; init; }
}

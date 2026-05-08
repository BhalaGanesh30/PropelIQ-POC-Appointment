namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Structured payload for a scheduling override audit event (EP-004 US_034 AC-2).
/// Stored verbatim in <see cref="PropelIQ.Modules.SharedServices.Domain.Entities.AuditRecord.Details"/>
/// Metadata to preserve forensic evidence per NFR-010.
/// </summary>
public sealed class OverrideAuditPayload
{
    public required Guid AppointmentId { get; init; }
    public required string ConstraintType { get; init; }
    public required string Reason { get; init; }
    public required string Action { get; init; }
    public required Guid OverrideRecordId { get; init; }
}

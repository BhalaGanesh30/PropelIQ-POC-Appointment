using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// EF Core implementation of <see cref="IAuditService"/>.
///
/// All writes are append-only to <c>app.audit_records</c> per NFR-010 and DR-005.
/// The <see cref="LogOverrideAsync"/> method adds the AuditRecord to the EF change
/// tracker and persists it within its own transaction so the override service can
/// wrap it inside its own transaction for atomicity (DR-002).
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Guid> LogOverrideAsync(
        OverrideAuditPayload payload,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new AuditRecord
        {
            EventType              = "Override",
            ActorUserId            = staffUserId,
            TargetEntityId         = payload.AppointmentId,
            TargetEntityType       = "Appointment",
            OccurredAt             = now,
            OverrideConstraintType = payload.ConstraintType,
            OverrideReason         = payload.Reason,
            OverrideAction         = payload.Action,
            Details = new AuditDetails
            {
                ChangeDescription = $"Scheduling constraint '{payload.ConstraintType}' overridden " +
                                    $"by staff {staffUserId} for appointment {payload.AppointmentId}.",
                Metadata = new Dictionary<string, string>
                {
                    ["constraintType"]   = payload.ConstraintType,
                    ["reason"]           = payload.Reason,
                    ["action"]           = payload.Action,
                    ["overrideRecordId"] = payload.OverrideRecordId.ToString(),
                    ["staffUserId"]      = staffUserId.ToString(),
                    ["occurredAt"]       = now.ToString("O"),
                },
            },
        };

        _db.AuditRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record.Id;
    }

    /// <inheritdoc />
    public async Task<Guid> LogStaffBookingAsync(
        StaffBookingAuditPayload payload,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var metadata = new Dictionary<string, string>
        {
            ["patientId"]            = payload.PatientId.ToString(),
            ["slotId"]               = payload.SlotId.ToString(),
            ["visitReason"]          = payload.VisitReason,
            ["inlinePatientCreated"] = payload.InlinePatientCreated.ToString(),
            ["staffUserId"]          = staffUserId.ToString(),
            ["occurredAt"]           = now.ToString("O"),
        };

        if (!string.IsNullOrWhiteSpace(payload.OverrideReason))
            metadata["overrideReason"] = payload.OverrideReason;

        var record = new AuditRecord
        {
            EventType        = "StaffBooking",
            ActorUserId      = staffUserId,
            TargetEntityId   = payload.AppointmentId,
            TargetEntityType = "Appointment",
            OccurredAt       = now,
            Details = new AuditDetails
            {
                ChangeDescription =
                    $"Staff {staffUserId} booked appointment {payload.AppointmentId} " +
                    $"on behalf of patient {payload.PatientId}.",
                Metadata = metadata,
            },
        };

        _db.AuditRecords.Add(record);
        // Note: SaveChangesAsync is called here so this can operate outside the
        // caller's transaction if needed; the caller (StaffBookingService) piggybacks
        // on its own SaveChangesAsync by calling this method before its own save.
        // The record is therefore added to the change tracker and persisted with
        // the appointment in the same SaveChangesAsync call.
        return record.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEntryDto>> GetAuditEntriesAsync(
        string? eventType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageSize = 50,
        int page = 0,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        var fromDate = from;
        var toDate   = to;

        // Left-join with Users to resolve actor display name and role.
        var query =
            from audit in _db.AuditRecords
            join user in _db.Users on audit.ActorUserId equals user.Id into userGroup
            from u in userGroup.DefaultIfEmpty()
            where eventType == null || audit.EventType == eventType
            where fromDate  == null || audit.OccurredAt >= fromDate.Value
            where toDate    == null || audit.OccurredAt <= toDate.Value
            orderby audit.OccurredAt descending
            select new
            {
                audit.Id,
                audit.EventType,
                audit.ActorUserId,
                ActorFirstName   = u.FirstName,
                ActorLastName    = u.LastName,
                ActorRole        = u.Role,
                audit.TargetEntityId,
                audit.TargetEntityType,
                audit.OccurredAt,
                Metadata         = audit.Details.Metadata,
            };

        var rows = await query
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return rows.Select(r => new AuditEntryDto
        {
            AuditId          = r.Id,
            EventType        = r.EventType,
            ActorUserId      = r.ActorUserId,
            ActorName        = (r.ActorFirstName != null || r.ActorLastName != null)
                                   ? $"{r.ActorFirstName} {r.ActorLastName}".Trim()
                                   : null,
            ActorRole        = r.ActorRole,
            TargetEntityId   = r.TargetEntityId,
            TargetEntityType = r.TargetEntityType,
            OccurredAt       = r.OccurredAt,
            Metadata         = r.Metadata,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> LogEventAsync(
        string eventType,
        Guid actorUserId,
        Guid? targetEntityId,
        string targetEntityType,
        Dictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var record = new AuditRecord
        {
            EventType        = eventType,
            ActorUserId      = actorUserId,
            TargetEntityId   = targetEntityId,
            TargetEntityType = targetEntityType,
            OccurredAt       = now,
            Details = new AuditDetails
            {
                ChangeDescription = $"Event '{eventType}' recorded by user {actorUserId} " +
                                    $"on {targetEntityType} {targetEntityId} at {now:O}.",
                Metadata = metadata,
            },
        };

        _db.AuditRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record.Id;
    }
}

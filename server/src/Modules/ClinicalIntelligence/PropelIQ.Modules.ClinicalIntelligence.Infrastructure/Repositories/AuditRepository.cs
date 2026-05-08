using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAuditRepository"/>.
/// Queries the shared <c>app.audit_records</c> table and left-joins <c>app.users</c>
/// to resolve the actor display name (AC-3).
/// </summary>
public sealed class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;

    public AuditRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FactAuditEntryDto>> GetByEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        var rows = await (
            from audit in _db.AuditRecords
            join user in _db.Users on audit.ActorUserId equals user.Id into userGroup
            from u in userGroup.DefaultIfEmpty()
            where audit.TargetEntityType == entityType
               && audit.TargetEntityId   == entityId
               && (audit.EventType == "fact_edited" || audit.EventType == "fact_verified")
            orderby audit.OccurredAt ascending
            select new
            {
                audit.Id,
                audit.EventType,
                audit.OccurredAt,
                audit.Details.Metadata,
                ActorFirstName = u.FirstName,
                ActorLastName  = u.LastName,
            }
        ).ToListAsync(ct);

        return rows.Select(r => new FactAuditEntryDto
        {
            AuditId             = r.Id,
            EventType           = r.EventType,
            PreviousName        = r.Metadata.GetValueOrDefault("previousName"),
            PreviousValue       = r.Metadata.GetValueOrDefault("previousValue"),
            EditorDisplayName   = (r.ActorFirstName != null || r.ActorLastName != null)
                                      ? $"{r.ActorFirstName} {r.ActorLastName}".Trim()
                                      : "Unknown",
            Timestamp           = r.OccurredAt,
        }).ToList();
    }
}

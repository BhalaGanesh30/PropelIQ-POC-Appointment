using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropelIQ.Api.Authorization.Policies;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin/Staff endpoint for querying patient data access logs (US_057, AC-4).
///
/// Returns a chronologically ordered, paginated list of DataAccess audit records
/// filtered by patient ID and optional date range.
///
/// The <c>patient_id</c> column added by migration 20260511120000 enables an
/// efficient B-tree index scan without JSONB containment overhead (NFR-010).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
[Route("api/v1/admin/access-logs")]
[ApiController]
[Produces("application/json")]
public sealed class AccessLogController : BaseApiController
{
    private readonly AppDbContext _db;

    public AccessLogController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns all DataAccess audit records for the given patient, filtered by an
    /// optional date range and paginated in chronological order (AC-4).
    ///
    /// Edge case 2: automated system-level accesses (actor role "System") are
    /// included and clearly distinguishable via the actorRole projection.
    /// </summary>
    /// <param name="patientId">Patient whose access history is requested (required).</param>
    /// <param name="fromUtc">Inclusive start of the date range filter (ISO-8601 UTC).</param>
    /// <param name="toUtc">Inclusive end of the date range filter (ISO-8601 UTC).</param>
    /// <param name="page">1-based page number. Default 1.</param>
    /// <param name="pageSize">Page size 1–200. Default 25.</param>
    [HttpGet]
    [ProducesResponseType(typeof(AccessLogPagedResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query(
        [FromQuery] Guid              patientId,
        [FromQuery] DateTimeOffset?   fromUtc     = null,
        [FromQuery] DateTimeOffset?   toUtc       = null,
        [FromQuery] int               page        = 1,
        [FromQuery] int               pageSize    = 25,
        CancellationToken             ct          = default)
    {
        if (pageSize is < 1 or > 200)
            return BadRequest(new { message = "pageSize must be between 1 and 200." });

        if (page < 1)
            return BadRequest(new { message = "page must be >= 1." });

        // AC-4: Filter by patient_id column (B-tree index) + DataAccess event type.
        var query = _db.AuditRecords
            .AsNoTracking()
            .Where(r => r.EventType == "DataAccess" && r.PatientId == patientId);

        if (fromUtc.HasValue)
            query = query.Where(r => r.OccurredAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(r => r.OccurredAt <= toUtc.Value);

        var total = await query.CountAsync(ct);

        // AC-4: Chronological ordering (ascending).
        var items = await query
            .Join(_db.Users,
                  r => r.ActorUserId,
                  u => u.Id,
                  (r, u) => new AccessLogEntryDto(
                      r.Id,
                      r.ActorUserId,
                      u.FirstName != null && u.LastName != null
                          ? $"{u.FirstName} {u.LastName}".Trim()
                          : null,
                      u.Role,
                      r.TargetEntityType,
                      r.TargetEntityId,
                      r.OccurredAt))
            .OrderBy(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new AccessLogPagedResult(total, items));
    }
}

// ── Projection DTOs ───────────────────────────────────────────────────────────

/// <summary>Single access log entry returned by the access log query.</summary>
/// <param name="AuditId">Internal audit record UUID.</param>
/// <param name="ActorUserId">UUID of the user who performed the access.</param>
/// <param name="ActorName">Full name of the actor (null for system accounts).</param>
/// <param name="ActorRole">JWT role of the actor (e.g., "Clinician", "Staff", "System").</param>
/// <param name="ResourceType">Controller name / entity type that was accessed.</param>
/// <param name="EntityId">UUID of the accessed entity (if captured).</param>
/// <param name="OccurredAt">UTC timestamp of the access event.</param>
public sealed record AccessLogEntryDto(
    Guid              AuditId,
    Guid              ActorUserId,
    string?           ActorName,
    string            ActorRole,
    string            ResourceType,
    Guid?             EntityId,
    DateTimeOffset    OccurredAt);

/// <summary>Paginated response wrapper for access log queries.</summary>
public sealed record AccessLogPagedResult(int Total, IReadOnlyList<AccessLogEntryDto> Items);

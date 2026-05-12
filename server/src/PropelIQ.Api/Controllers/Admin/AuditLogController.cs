using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin-only audit log query and CSV export API (US_056, AC-3, Edge Case 2).
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>GET  /api/v1/admin/audit-logs</c>  — paginated/filtered query.</item>
///   <item><c>POST /api/v1/admin/audit-logs/export</c> — trigger async CSV export (202).</item>
///   <item><c>GET  /api/v1/admin/audit-logs/export/{jobId}</c> — poll / download CSV.</item>
/// </list>
///
/// NFR-010: All queries are read-only; no mutation of audit records is possible.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/audit-logs")]
[ApiController]
[Produces("application/json")]
public sealed class AuditLogController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly AuditLogExportService _exportService;

    public AuditLogController(AppDbContext db, AuditLogExportService exportService)
    {
        _db            = db;
        _exportService = exportService;
    }

    /// <summary>
    /// Returns a paginated list of audit records filtered by optional criteria.
    ///
    /// AC-3: Filterable by actor, action type, date range, and entity ID.
    /// </summary>
    /// <param name="actorUserId">Filter by exact actor user UUID.</param>
    /// <param name="eventType">Filter by event type string (e.g., DataAccess, ConfigChanged).</param>
    /// <param name="from">Inclusive lower bound (ISO-8601 UTC).</param>
    /// <param name="to">Inclusive upper bound (ISO-8601 UTC).</param>
    /// <param name="entityId">Filter by target entity UUID.</param>
    /// <param name="page">Zero-based page number. Default 0.</param>
    /// <param name="pageSize">Page size 1–200. Default 50.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid?   actorUserId = null,
        [FromQuery] string? eventType   = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to   = null,
        [FromQuery] Guid?   entityId    = null,
        [FromQuery] int     page        = 0,
        [FromQuery] int     pageSize    = 50,
        CancellationToken   ct          = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Rename locals to avoid conflict with the LINQ 'from' contextual keyword.
        var fromDate = from;
        var toDate   = to;

        var query =
            from audit in _db.AuditRecords
            join user in _db.Users on audit.ActorUserId equals user.Id into userGroup
            from u in userGroup.DefaultIfEmpty()
            where actorUserId == null || audit.ActorUserId    == actorUserId.Value
            where eventType   == null || audit.EventType      == eventType
            where fromDate    == null || audit.OccurredAt     >= fromDate.Value
            where toDate      == null || audit.OccurredAt     <= toDate.Value
            where entityId    == null || audit.TargetEntityId == entityId.Value
            orderby audit.OccurredAt descending
            select new AuditEntryDto
            {
                AuditId          = audit.Id,
                EventType        = audit.EventType,
                ActorUserId      = audit.ActorUserId,
                ActorName        = (u.FirstName != null || u.LastName != null)
                                       ? $"{u.FirstName} {u.LastName}".Trim()
                                       : null,
                ActorRole        = u.Role,
                TargetEntityId   = audit.TargetEntityId,
                TargetEntityType = audit.TargetEntityType,
                OccurredAt       = audit.OccurredAt,
                Metadata         = audit.Details.Metadata,
            };

        var results = await query
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(results);
    }

    /// <summary>
    /// Triggers an asynchronous CSV export for the given filters.
    /// Returns 202 Accepted with the job ID for polling.
    ///
    /// Edge Case 2: Large exports are generated asynchronously to avoid request timeout.
    /// </summary>
    [HttpPost("export")]
    [ProducesResponseType(typeof(ExportStartedResponse), StatusCodes.Status202Accepted)]
    public IActionResult StartExport([FromBody] AuditLogQueryRequest request)
    {
        var jobId = _exportService.StartExport(request);

        return AcceptedAtAction(
            nameof(DownloadExport),
            new { jobId },
            new ExportStartedResponse(jobId));
    }

    /// <summary>
    /// Polls an export job.
    /// Returns 200 with the CSV file when ready, 202 while still processing, or 404 when expired/not found.
    ///
    /// Download links expire after 1 hour (Edge Case 2).
    /// </summary>
    [HttpGet("export/{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadExport(Guid jobId)
    {
        var (found, ready, csvBytes) = _exportService.TryGetResult(jobId);

        if (!found)
            return NotFound(new { message = "Export job not found or has expired." });

        if (!ready)
            return AcceptedAtAction(nameof(DownloadExport), new { jobId }, new { jobId, status = "pending" });

        var filename = $"audit-log-export-{jobId:N}.csv";
        return File(csvBytes!, "text/csv", filename);
    }

    /// <summary>Response body for a started export job.</summary>
    public sealed record ExportStartedResponse(Guid JobId);
}

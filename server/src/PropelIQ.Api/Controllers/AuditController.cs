using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Audit log query API (EP-004 US_034 AC-4).
///
/// GET /api/v1/audit — returns paginated audit records optionally filtered by
/// action type, date range, and page parameters.
///
/// AC-4: <c>?actionType=Override</c> returns all scheduling override events with
///       full reason, actor identity, and target appointment details.
///
/// Restricted to the Admin role — regular Staff members do not have access to
/// the raw audit log (principle of least privilege, OWASP AC).
///
/// NFR-010: Audit records are append-only (never updated or deleted); this
///          endpoint is read-only.
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/audit")]
[ApiController]
[Produces("application/json")]
public sealed class AuditController : BaseApiController
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Returns paginated audit records with optional filtering by event type and date range.
    /// </summary>
    /// <param name="actionType">
    /// Filter by event type (e.g., <c>Override</c>, <c>AppointmentStateTransition</c>).
    /// Omit to return all event types.
    /// </param>
    /// <param name="from">
    /// Inclusive start of the date range filter (ISO-8601 UTC).
    /// Omit for no lower bound.
    /// </param>
    /// <param name="to">
    /// Inclusive end of the date range filter (ISO-8601 UTC).
    /// Omit for no upper bound.
    /// </param>
    /// <param name="pageSize">Number of records per page (1–200, default 50).</param>
    /// <param name="page">0-based page index (default 0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of audit entries ordered by <c>OccurredAt</c> descending.</returns>
    /// <response code="200">Audit entries returned (may be empty).</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have the Admin role.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditEntries(
        [FromQuery] string? actionType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageSize = 50,
        [FromQuery] int page     = 0,
        CancellationToken ct     = default)
    {
        var results = await _auditService.GetAuditEntriesAsync(
            eventType: actionType,
            from:      from,
            to:        to,
            pageSize:  pageSize,
            page:      page,
            ct:        ct);

        return Ok(results);
    }
}

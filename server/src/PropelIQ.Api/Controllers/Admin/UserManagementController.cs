using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Modules.SharedServices.Application.Administration;

namespace PropelIQ.Api.Controllers.Admin;

/// <summary>
/// Admin-only user lifecycle management REST API (US_061, AC-1–AC-4, edge cases 1–2).
///
/// <list type="bullet">
///   <item><c>GET  /api/v1/admin/users</c>                                 — paginated, searchable user list (AC-1).</item>
///   <item><c>GET  /api/v1/admin/users/{userId}</c>                        — individual user detail.</item>
///   <item><c>POST /api/v1/admin/users/bulk</c>                            — bulk activate/deactivate/assign-role (AC-2, AC-4).</item>
///   <item><c>GET  /api/v1/admin/users/{userId}/activity</c>               — reverse-chronological activity history (AC-3).</item>
/// </list>
///
/// <para>All endpoints require the <c>Admin</c> role (US_015 authorization infrastructure).</para>
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/users")]
[ApiController]
[Produces("application/json")]
public sealed class UserManagementController : BaseApiController
{
    private readonly IUserManagementService             _svc;
    private readonly IValidator<BulkActionRequest>      _bulkValidator;

    public UserManagementController(
        IUserManagementService          svc,
        IValidator<BulkActionRequest>   bulkValidator)
    {
        _svc           = svc;
        _bulkValidator = bulkValidator;
    }

    /// <summary>
    /// Returns a paginated list of users with optional name/email search and
    /// role/status filters (AC-1).
    /// </summary>
    /// <param name="searchTerm">Partial match on full name or email address.</param>
    /// <param name="roleFilter">Exact role value (e.g. <c>Admin</c>, <c>Staff</c>).</param>
    /// <param name="statusFilter"><c>Active</c> or <c>Inactive</c>. Omit for all users.</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Rows per page; capped at 100 (default: 25).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserListItem>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> List(
        [FromQuery] string? searchTerm  = null,
        [FromQuery] string? roleFilter  = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] int     page        = 1,
        [FromQuery] int     pageSize    = 25,
        CancellationToken               ct = default)
    {
        var query  = new UserListQuery(searchTerm, roleFilter, statusFilter, page, pageSize);
        var result = await _svc.ListAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Returns full profile for a single user by their domain ID.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserDetailDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid userId, CancellationToken ct)
    {
        try
        {
            var result = await _svc.GetByIdAsync(userId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = $"User {userId} not found." });
        }
    }

    /// <summary>
    /// Applies a bulk Activate, Deactivate, or AssignRole operation to up to 200 users
    /// in a single call (AC-2, AC-4).
    ///
    /// <para>
    /// Returns <c>200</c> when at least one user was processed successfully (with partial
    /// failure details in the body). Returns <c>422</c> when every user failed (e.g. the
    /// last-admin guard blocked the entire Deactivate request).
    /// </para>
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkActionResult), 200)]
    [ProducesResponseType(typeof(IEnumerable<string>), 400)]
    [ProducesResponseType(typeof(BulkActionResult), 422)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> BulkAction(
        [FromBody] BulkActionRequest request,
        CancellationToken            ct)
    {
        var validation = await _bulkValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var adminId = TryGetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Admin user identity could not be resolved.");

        var result = await _svc.BulkActionAsync(request, adminId, ct);

        // If every item failed (e.g. last-admin guard blocked the entire operation) → 422.
        if (result.SuccessCount == 0 && result.FailureCount > 0)
            return UnprocessableEntity(result);

        return Ok(result);
    }

    /// <summary>
    /// Returns the reverse-chronological activity history for a specific user (AC-3).
    /// History includes login events, role changes, and admin-performed actions.
    /// </summary>
    [HttpGet("{userId:guid}/activity")]
    [ProducesResponseType(typeof(IReadOnlyList<UserActivityEntry>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetActivityHistory(
        Guid              userId,
        [FromQuery] int   page     = 1,
        [FromQuery] int   pageSize = 25,
        CancellationToken ct       = default)
    {
        var result = await _svc.GetActivityHistoryAsync(userId, page, pageSize, ct);
        return Ok(result);
    }
}

namespace PropelIQ.Modules.SharedServices.Application.Administration;

// ── Queries ───────────────────────────────────────────────────────────────────

/// <summary>
/// Paginated listing query for the user management grid (US_061, AC-1).
/// </summary>
/// <param name="SearchTerm">Optional text matched against full name or email (case-insensitive).</param>
/// <param name="RoleFilter">Optional role value to narrow results (e.g. "Admin", "Staff").</param>
/// <param name="StatusFilter">
/// Optional active-state filter: <c>"Active"</c> (IsActive = true) or <c>"Inactive"</c> (IsActive = false).
/// Null returns all users.
/// </param>
/// <param name="Page">1-based page number (default: 1).</param>
/// <param name="PageSize">Number of rows per page; capped at 100 (default: 25).</param>
public sealed record UserListQuery(
    string? SearchTerm    = null,
    string? RoleFilter    = null,
    string? StatusFilter  = null,
    int     Page          = 1,
    int     PageSize      = 25);

// ── Response models ───────────────────────────────────────────────────────────

/// <summary>
/// Summary row returned in the paginated user list (AC-1).
/// </summary>
public sealed record UserListItem(
    Guid      UserId,
    string    Name,
    string    Email,
    string    Role,
    bool      IsActive,
    DateTimeOffset? LastLoginAt);

/// <summary>
/// Full user detail returned by <c>GET /api/v1/admin/users/{userId}</c>.
/// </summary>
public sealed record UserDetailDto(
    Guid      UserId,
    string    Name,
    string    Email,
    string    Role,
    bool      IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset  CreatedAt);

/// <summary>Generic page envelope shared by all paginated endpoints.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int              TotalCount,
    int              Page,
    int              PageSize);

// ── Bulk action ───────────────────────────────────────────────────────────────

/// <summary>
/// Type of bulk operation to apply (AC-2).
/// </summary>
public enum BulkActionType
{
    Activate    = 0,
    Deactivate  = 1,
    AssignRole  = 2
}

/// <summary>
/// Request payload for <c>POST /api/v1/admin/users/bulk</c> (AC-2, AC-4).
/// </summary>
/// <param name="UserIds">IDs of the target users. Max 200 per call (enforced by validator).</param>
/// <param name="Action">Operation to perform.</param>
/// <param name="TargetRole">Required only when <see cref="Action"/> is <see cref="BulkActionType.AssignRole"/>.</param>
public sealed record BulkActionRequest(
    IReadOnlyList<Guid> UserIds,
    BulkActionType      Action,
    string?             TargetRole = null);

/// <summary>
/// Aggregate outcome of a bulk operation (AC-4).
/// </summary>
/// <param name="SuccessCount">Number of users successfully processed.</param>
/// <param name="FailureCount">Number of users that could not be processed.</param>
/// <param name="Failures">Details of each failure (userId, display name, reason).</param>
public sealed record BulkActionResult(
    int                       SuccessCount,
    int                       FailureCount,
    IReadOnlyList<BulkActionFailure> Failures);

/// <summary>
/// Per-user failure detail included in <see cref="BulkActionResult"/> (AC-4).
/// </summary>
public sealed record BulkActionFailure(
    Guid   UserId,
    string UserName,
    string Reason);

// ── Activity history ──────────────────────────────────────────────────────────

/// <summary>
/// Single activity event returned by the per-user activity history endpoint (AC-3).
///
/// <para>
/// Backed by <c>app.audit_records</c> (via <c>TargetEntityId = userId</c>) until the
/// dedicated <c>user_activity_log</c> table is introduced by US_061 task_002,
/// at which point this query will be updated transparently.
/// </para>
/// </summary>
public sealed record UserActivityEntry(
    Guid      Id,
    string    EventType,
    string    Description,
    DateTimeOffset OccurredAt,
    string?   PerformedByUserId);

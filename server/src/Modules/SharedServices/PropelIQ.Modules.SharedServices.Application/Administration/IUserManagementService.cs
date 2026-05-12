namespace PropelIQ.Modules.SharedServices.Application.Administration;

/// <summary>
/// Contract for the admin user lifecycle management service (US_061).
///
/// Provides paginated listing with search/filter, individual user detail,
/// bulk activate/deactivate/assign-role operations, and per-user activity history.
///
/// All mutating operations write audit records via <c>IAuditRecordService</c> (NFR-010).
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Returns a paginated, searchable list of all users (AC-1).
    /// Supports full-name and email search, plus role and active-status filters.
    /// </summary>
    Task<PagedResult<UserListItem>> ListAsync(
        UserListQuery      query,
        CancellationToken  ct = default);

    /// <summary>
    /// Returns the full profile of a single user by their domain <c>Id</c>.
    /// Throws <see cref="KeyNotFoundException"/> when the user does not exist.
    /// </summary>
    Task<UserDetailDto> GetByIdAsync(
        Guid              userId,
        CancellationToken ct = default);

    /// <summary>
    /// Applies a single bulk action (Activate, Deactivate, AssignRole) to a
    /// set of users in one database round-trip (AC-2, AC-4).
    ///
    /// <para>
    /// Individual validation failures (last-admin guard, invalid role mapping) are
    /// captured in <see cref="BulkActionResult.Failures"/> without rolling back
    /// other successful updates.
    /// </para>
    /// </summary>
    /// <param name="request">Operation type and target user IDs.</param>
    /// <param name="adminId">User ID of the authenticated admin who issued the request.</param>
    Task<BulkActionResult> BulkActionAsync(
        BulkActionRequest  request,
        Guid               adminId,
        CancellationToken  ct = default);

    /// <summary>
    /// Returns the reverse-chronological activity history for a specific user (AC-3).
    ///
    /// <para>
    /// Currently backed by <c>app.audit_records</c> (filtered by <c>target_entity_id = userId</c>).
    /// Will be updated to query <c>app.user_activity_logs</c> once US_061 task_002
    /// introduces the dedicated table.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserActivityEntry>> GetActivityHistoryAsync(
        Guid              userId,
        int               page     = 1,
        int               pageSize = 25,
        CancellationToken ct       = default);
}

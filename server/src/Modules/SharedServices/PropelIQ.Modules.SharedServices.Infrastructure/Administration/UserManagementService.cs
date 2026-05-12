using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Administration;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Administration;

/// <summary>
/// Scoped service implementing admin user lifecycle management (US_061, AC-1–AC-4).
///
/// <para>
/// All mutations (Activate, Deactivate, AssignRole) are accumulated via EF Core change tracking
/// and persisted in a single <see cref="AppDbContext.SaveChangesAsync"/> call, giving
/// atomic commit semantics across the batch while allowing per-user validation failures to
/// be captured without rolling back already-validated updates (AC-4).
/// </para>
///
/// <para>
/// Every successful mutation emits an <see cref="AuditEvent"/> via the channel-based
/// <see cref="IAuditRecordService"/> (NFR-010, US_056 AC-1).
/// </para>
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
    private readonly AppDbContext                  _db;
    private readonly IAuditRecordService           _audit;
    private readonly ILogger<UserManagementService> _log;

    public UserManagementService(
        AppDbContext                   db,
        IAuditRecordService            audit,
        ILogger<UserManagementService> log)
    {
        _db    = db;
        _audit = audit;
        _log   = log;
    }

    // ── Allowed role transitions — keyed by current user Role (serves as user-type proxy) ──
    // Edge case 2: role assignment validated against this mapping; invalid → descriptive error.
    private static readonly Dictionary<string, HashSet<string>> AllowedRoleTransitions =
        new(StringComparer.Ordinal)
        {
            ["Patient"]   = ["Patient"],
            ["Staff"]     = ["Staff", "Clinician"],
            ["Clinician"] = ["Clinician", "Staff"],
            ["Admin"]     = ["Admin", "Staff", "Clinician"],
        };

    /// <inheritdoc/>
    public async Task<PagedResult<UserListItem>> ListAsync(
        UserListQuery      query,
        CancellationToken  ct = default)
    {
        var q = _db.Users.AsNoTracking();

        // Search by name or email (AC-1).
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            q = q.Where(u =>
                u.Email.ToLower().Contains(term) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.RoleFilter))
            q = q.Where(u => u.Role == query.RoleFilter);

        if (!string.IsNullOrWhiteSpace(query.StatusFilter))
        {
            var active = string.Equals(query.StatusFilter, "Active", StringComparison.OrdinalIgnoreCase);
            q = q.Where(u => u.IsActive == active);
        }

        var total = await q.CountAsync(ct);

        // Clamp page size to reasonable maximum.
        var pageSize = Math.Min(query.PageSize, 100);

        var items = await q
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItem(
                u.Id,
                (u.FirstName ?? string.Empty) + " " + (u.LastName ?? string.Empty),
                u.Email,
                u.Role,
                u.IsActive,
                u.LastLoginAt))
            .ToListAsync(ct);

        return new PagedResult<UserListItem>(items, total, query.Page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<UserDetailDto> GetByIdAsync(
        Guid              userId,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        return new UserDetailDto(
            user.Id,
            (user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty),
            user.Email,
            user.Role,
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt);
    }

    /// <inheritdoc/>
    public async Task<BulkActionResult> BulkActionAsync(
        BulkActionRequest  request,
        Guid               adminId,
        CancellationToken  ct = default)
    {
        var failures = new List<BulkActionFailure>();

        // Load all target users in one query to avoid N+1 reads.
        var users = await _db.Users
            .Where(u => request.UserIds.Contains(u.Id))
            .ToListAsync(ct);

        // ── Last-admin guard (edge case 1) ─────────────────────────────────────
        // Pre-validate before touching any entity: count active admins that would
        // remain after deactivating all requested admin users.
        if (request.Action == BulkActionType.Deactivate)
        {
            var activeAdminCount     = await _db.Users.CountAsync(u => u.Role == "Admin" && u.IsActive, ct);
            var adminsToDeactivate   = users.Count(u => u.Role == "Admin" && u.IsActive);
            var remainingAdminCount  = activeAdminCount - adminsToDeactivate;

            if (remainingAdminCount < 1)
            {
                return new BulkActionResult(
                    SuccessCount: 0,
                    FailureCount: request.UserIds.Count,
                    Failures: [new BulkActionFailure(
                        Guid.Empty,
                        "All selected admin accounts",
                        "Cannot deactivate all admin accounts. At least one admin must remain active.")]);
            }
        }

        // ── Per-user processing ────────────────────────────────────────────────
        var successCount = 0;

        foreach (var user in users)
        {
            var displayName = $"{user.FirstName} {user.LastName}".Trim();

            try
            {
                switch (request.Action)
                {
                    case BulkActionType.Activate:
                        user.IsActive = true;
                        break;

                    case BulkActionType.Deactivate:
                        user.IsActive = false;
                        break;

                    case BulkActionType.AssignRole:
                        // Edge case 2: validate role against allowed transitions for current user role.
                        if (!IsRoleTransitionAllowed(user.Role, request.TargetRole!))
                        {
                            failures.Add(new BulkActionFailure(
                                user.Id,
                                displayName,
                                $"Role '{request.TargetRole}' cannot be assigned to a user currently holding the '{user.Role}' role."));
                            continue;
                        }
                        user.Role = request.TargetRole!;
                        break;

                    default:
                        failures.Add(new BulkActionFailure(user.Id, displayName, "Unknown bulk action type."));
                        continue;
                }

                // Emit audit record for each successful mutation (NFR-010).
                await _audit.WriteAsync(new AuditEvent
                {
                    UserId         = adminId,
                    EventType      = request.Action switch
                    {
                        BulkActionType.Activate   => "UserActivated",
                        BulkActionType.Deactivate => "UserDeactivated",
                        BulkActionType.AssignRole => "RoleAssigned",
                        _                         => "UserBulkAction"
                    },
                    EntityType  = "User",
                    EntityId    = user.Id,
                    Details     = new Dictionary<string, object>
                    {
                        ["Action"]     = request.Action.ToString(),
                        ["TargetRole"] = request.TargetRole ?? string.Empty
                    }
                }, ct);

                // Write dedicated activity log entry visible in the user's history panel (AC-3).
                _db.UserActivityLogs.Add(new UserActivityLog
                {
                    UserId             = user.Id,
                    EventType          = request.Action switch
                    {
                        BulkActionType.Activate   => "UserActivated",
                        BulkActionType.Deactivate => "UserDeactivated",
                        BulkActionType.AssignRole => "RoleAssigned",
                        _                         => "BulkAction"
                    },
                    Description        = request.Action switch
                    {
                        BulkActionType.Activate   => "Account activated by administrator.",
                        BulkActionType.Deactivate => "Account deactivated by administrator.",
                        BulkActionType.AssignRole => $"Role assigned to '{request.TargetRole}'.",
                        _                         => "Bulk action performed."
                    },
                    PerformedByUserId  = adminId
                });

                successCount++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Bulk action {Action} failed for user {UserId}", request.Action, user.Id);

                failures.Add(new BulkActionFailure(
                    user.Id,
                    displayName,
                    "An unexpected error occurred processing this user."));
            }
        }

        // Persist all successful mutations in a single round-trip (AC-4).
        if (successCount > 0)
            await _db.SaveChangesAsync(ct);

        return new BulkActionResult(successCount, failures.Count, failures);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserActivityEntry>> GetActivityHistoryAsync(
        Guid              userId,
        int               page     = 1,
        int               pageSize = 25,
        CancellationToken ct       = default)
    {
        // Query the dedicated user_activity_logs table (US_061 task_002).
        // Ordered by OccurredAt descending for reverse chronological display (AC-3).
        return await _db.UserActivityLogs
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new UserActivityEntry(
                a.Id,
                a.EventType,
                a.Description,
                a.OccurredAt,
                a.PerformedByUserId.HasValue
                    ? a.PerformedByUserId.Value.ToString()
                    : null))
            .ToListAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="targetRole"/> is a permitted assignment
    /// for a user whose current role is <paramref name="currentRole"/> (edge case 2).
    /// Falls back to allowing any known system role when the current role is unrecognised.
    /// </summary>
    private static bool IsRoleTransitionAllowed(string currentRole, string targetRole)
    {
        if (AllowedRoleTransitions.TryGetValue(currentRole, out var allowed))
            return allowed.Contains(targetRole, StringComparer.Ordinal);

        // Unrecognised current role: permit any standard system role as a recovery path.
        return AllowedRoleTransitions.Values.Any(set => set.Contains(targetRole, StringComparer.Ordinal));
    }
}

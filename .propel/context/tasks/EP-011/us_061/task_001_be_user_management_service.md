# Task - TASK_001

## Requirement Reference

- User Story: us_061
- Story Location: .propel/context/tasks/EP-011/us_061/us_061.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I navigate to user management, Then all users are listed with name, email, role, status, and last active date with pagination and search by name/email.
  - AC-2: Given I select multiple users using checkboxes, When I apply a bulk action (Activate, Deactivate, or Assign Role), Then the action is applied to all selected users in a single operation and each change is recorded in the audit log.
  - AC-3: Given I view a specific user's profile, When I open their activity history, Then recent login events, role changes, and actions performed are listed in reverse chronological order.
  - AC-4: Given I bulk deactivate 50 users, When the operation completes, Then a summary confirmation shows "50 users deactivated" and lists any users where the action failed (e.g., attempting to deactivate the last admin).
- Edge Cases:
  - What happens if a bulk action would deactivate all admin accounts? System validates the action and blocks it with: "Cannot deactivate all admin accounts. At least one admin must remain active."
  - How does the system handle role assignment to a user type that doesn't support the role? Role assignment is validated against allowed role-user-type mappings; invalid assignments return a descriptive error.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL | 15.x |
| Library | FluentValidation | latest stable |
| Library | Polly | latest stable |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the backend user lifecycle administration service exposing REST endpoints for user CRUD, paginated listing with search, bulk actions (activate, deactivate, assign role), and per-user activity history. The `IUserManagementService` contract provides `ListAsync` with pagination, name/email search, and role/status filters (AC-1); `BulkActionAsync` applying a single operation to a set of user IDs with per-user success/failure tracking (AC-2, AC-4); and `GetActivityHistoryAsync` returning login events, role changes, and actions in reverse chronological order (AC-3). A `LastAdminGuard` validation rule counts remaining active admins before any deactivation and returns a 422 error if the operation would leave zero active admins (edge case 1). Role-assignment validation checks an allowed role-user-type mapping dictionary and returns descriptive errors for invalid combinations (edge case 2). Every mutation (activate, deactivate, role change) writes an audit record via `IAuditRecordService` (NFR-010). Bulk operations execute within a single database transaction using `IDbContextTransaction`; individual failures are captured and returned in a `BulkActionResult` summary without rolling back successful items (AC-4). All endpoints require Admin role authorization.

## Dependent Tasks

- US_061 task_002 (requires `user_activity_log` table for activity history)
- US_056 task_001 (requires `IAuditRecordService` for audit logging)
- US_016 task_001 (requires staff account invitation infrastructure)
- US_015 task_001 (requires Admin authorization infrastructure)

## Impacted Components

- New: `server/src/PropelIQ.Application/Interfaces/IUserManagementService.cs` (service contract)
- New: `server/src/PropelIQ.Application/Models/UserAdmin/UserAdminModels.cs` (DTOs)
- New: `server/src/PropelIQ.Application/Services/UserManagementService.cs` (service implementation)
- New: `server/src/PropelIQ.Application/Validators/BulkActionValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Application/Validators/RoleAssignmentValidator.cs` (role-type mapping)
- New: `server/src/PropelIQ.Api/Controllers/UserManagementController.cs` (REST endpoints)

## Implementation Plan

1. **Define the service contract and DTOs**:

```csharp
// PropelIQ.Application/Interfaces/
//   IUserManagementService.cs
public interface IUserManagementService
{
    Task<PagedResult<UserListItem>> ListAsync(
        UserListQuery query,
        CancellationToken ct = default);

    Task<UserDetailDto> GetByIdAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<BulkActionResult> BulkActionAsync(
        BulkActionRequest request,
        Guid adminId,
        CancellationToken ct = default);

    Task<IReadOnlyList<UserActivityEntry>>
        GetActivityHistoryAsync(
            Guid userId, int page, int pageSize,
            CancellationToken ct = default);
}

// PropelIQ.Application/Models/UserAdmin/
//   UserAdminModels.cs
public sealed record UserListQuery(
    string? SearchTerm,
    string? RoleFilter,
    string? StatusFilter,
    int Page = 1,
    int PageSize = 25);

public sealed record UserListItem(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    string Status,
    DateTime? LastActiveUtc);

public sealed record UserDetailDto(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    string Status,
    DateTime? LastActiveUtc,
    DateTime CreatedAtUtc);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public enum BulkActionType
{
    Activate,
    Deactivate,
    AssignRole
}

public sealed record BulkActionRequest(
    IReadOnlyList<Guid> UserIds,
    BulkActionType Action,
    string? TargetRole = null);

public sealed record BulkActionResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<BulkActionFailure> Failures);

public sealed record BulkActionFailure(
    Guid UserId,
    string UserName,
    string Reason);

public sealed record UserActivityEntry(
    Guid Id,
    string EventType,
    string Description,
    DateTime OccurredAtUtc,
    string? PerformedByName);
```

2. **Implement `UserManagementService`** with paginated listing, bulk actions, and last-admin guard:

```csharp
// PropelIQ.Application/Services/
//   UserManagementService.cs
public sealed class UserManagementService
    : IUserManagementService
{
    private readonly AppDbContext _db;
    private readonly IAuditRecordService _audit;
    private readonly ILogger<UserManagementService>
        _log;

    // Constructor injection omitted for brevity

    public async Task<PagedResult<UserListItem>>
        ListAsync(
            UserListQuery query,
            CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(
            query.SearchTerm))
        {
            var term = query.SearchTerm.ToLower();
            q = q.Where(u =>
                u.Email.ToLower().Contains(term)
                || (u.FirstName + " " + u.LastName)
                    .ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(
            query.RoleFilter))
            q = q.Where(u =>
                u.Role == query.RoleFilter);

        if (!string.IsNullOrWhiteSpace(
            query.StatusFilter))
            q = q.Where(u =>
                u.Status == query.StatusFilter);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserListItem(
                u.UserId,
                u.FirstName + " " + u.LastName,
                u.Email,
                u.Role,
                u.Status,
                u.LastLogin))
            .ToListAsync(ct);

        return new PagedResult<UserListItem>(
            items, total,
            query.Page, query.PageSize);
    }

    public async Task<BulkActionResult>
        BulkActionAsync(
            BulkActionRequest request,
            Guid adminId,
            CancellationToken ct)
    {
        var successes = 0;
        var failures =
            new List<BulkActionFailure>();

        var users = await _db.Users
            .Where(u =>
                request.UserIds.Contains(u.UserId))
            .ToListAsync(ct);

        // Last-admin guard (edge case 1)
        if (request.Action ==
            BulkActionType.Deactivate)
        {
            var activeAdminCount = await _db.Users
                .CountAsync(u =>
                    u.Role == "Admin"
                    && u.Status == "Active", ct);
            var adminsToDeactivate = users
                .Count(u => u.Role == "Admin"
                    && u.Status == "Active");

            if (activeAdminCount
                - adminsToDeactivate < 1)
            {
                return new BulkActionResult(
                    0, request.UserIds.Count,
                    new[]
                    {
                        new BulkActionFailure(
                            Guid.Empty,
                            "All selected admins",
                            "Cannot deactivate all "
                            + "admin accounts. At "
                            + "least one admin must "
                            + "remain active.")
                    });
            }
        }

        foreach (var user in users)
        {
            try
            {
                switch (request.Action)
                {
                    case BulkActionType.Activate:
                        user.Status = "Active";
                        break;

                    case BulkActionType.Deactivate:
                        // Per-user last-admin check
                        if (user.Role == "Admin")
                        {
                            var remaining =
                                await _db.Users
                                    .CountAsync(u =>
                                        u.Role ==
                                            "Admin"
                                        && u.Status ==
                                            "Active"
                                        && u.UserId !=
                                            user
                                            .UserId,
                                        ct);
                            if (remaining < 1)
                            {
                                failures.Add(
                                    new BulkActionFailure(
                                        user.UserId,
                                        user.FirstName
                                        + " "
                                        + user.LastName,
                                        "Cannot "
                                        + "deactivate "
                                        + "the last "
                                        + "active "
                                        + "admin."));
                                continue;
                            }
                        }
                        user.Status = "Inactive";
                        break;

                    case BulkActionType.AssignRole:
                        if (!IsValidRoleAssignment(
                            user, request.TargetRole!))
                        {
                            failures.Add(
                                new BulkActionFailure(
                                    user.UserId,
                                    user.FirstName
                                    + " "
                                    + user.LastName,
                                    $"Role "
                                    + $"'{request"
                                    + $".TargetRole}'"
                                    + $" is not valid"
                                    + $" for user type"
                                    + $" '{user"
                                    + $".UserType}'."
                                    ));
                            continue;
                        }
                        user.Role =
                            request.TargetRole!;
                        break;
                }

                await _audit.WriteAsync(
                    new AuditEvent(
                        adminId,
                        $"User{request.Action}",
                        "User",
                        user.UserId,
                        new { Action = request.Action,
                              UserId = user.UserId }),
                    ct);
                successes++;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Bulk action failed for user "
                    + "{UserId}", user.UserId);
                failures.Add(
                    new BulkActionFailure(
                        user.UserId,
                        user.FirstName + " "
                        + user.LastName,
                        "Unexpected error."));
            }
        }

        await _db.SaveChangesAsync(ct);

        return new BulkActionResult(
            successes, failures.Count, failures);
    }

    public async Task<
        IReadOnlyList<UserActivityEntry>>
        GetActivityHistoryAsync(
            Guid userId, int page, int pageSize,
            CancellationToken ct)
    {
        return await _db.UserActivityLogs
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a =>
                a.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new UserActivityEntry(
                a.Id,
                a.EventType,
                a.Description,
                a.OccurredAtUtc,
                a.PerformedByName))
            .ToListAsync(ct);
    }

    private static readonly Dictionary<string,
        HashSet<string>> AllowedRoleMappings =
        new()
        {
            ["Patient"] = new() { "Patient" },
            ["Staff"] = new()
                { "Staff", "FrontDesk" },
            ["Clinician"] = new()
                { "Clinician", "Staff" },
            ["Admin"] = new()
                { "Admin", "Staff", "Clinician" }
        };

    private static bool IsValidRoleAssignment(
        dynamic user, string targetRole)
    {
        var userType =
            (string)(user.UserType ?? "Staff");
        return AllowedRoleMappings
            .TryGetValue(userType,
                out var allowed)
            && allowed.Contains(targetRole);
    }
}
```

3. **Implement `BulkActionValidator`** with FluentValidation:

```csharp
// PropelIQ.Application/Validators/
//   BulkActionValidator.cs
public sealed class BulkActionValidator
    : AbstractValidator<BulkActionRequest>
{
    public BulkActionValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty()
            .WithMessage(
                "At least one user must be "
                + "selected.")
            .Must(ids => ids.Count <= 200)
            .WithMessage(
                "Maximum 200 users per bulk "
                + "action.");

        RuleFor(x => x.Action)
            .IsInEnum()
            .WithMessage(
                "Invalid bulk action type.");

        When(
            x => x.Action ==
                BulkActionType.AssignRole,
            () =>
            {
                RuleFor(x => x.TargetRole)
                    .NotEmpty()
                    .WithMessage(
                        "Target role is required "
                        + "for role assignment.");
            });
    }
}
```

4. **Implement `UserManagementController`** with Admin-only endpoints:

```csharp
// PropelIQ.Api/Controllers/
//   UserManagementController.cs
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class UserManagementController
    : ControllerBase
{
    private readonly IUserManagementService _svc;
    private readonly IValidator<BulkActionRequest>
        _bulkValidator;

    // Constructor injection omitted for brevity

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] UserListQuery query,
        CancellationToken ct)
    {
        var result = await _svc
            .ListAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetById(
        Guid userId, CancellationToken ct)
    {
        var result = await _svc
            .GetByIdAsync(userId, ct);
        return Ok(result);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkAction(
        [FromBody] BulkActionRequest request,
        CancellationToken ct)
    {
        var validation = await _bulkValidator
            .ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors
                .Select(e => e.ErrorMessage));

        var adminId = GetAdminId();
        var result = await _svc
            .BulkActionAsync(
                request, adminId, ct);

        if (result.FailureCount > 0
            && result.SuccessCount == 0)
            return UnprocessableEntity(result);

        return Ok(result);
    }

    [HttpGet("{userId:guid}/activity")]
    public async Task<IActionResult>
        GetActivityHistory(
            Guid userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
    {
        var result = await _svc
            .GetActivityHistoryAsync(
                userId, page, pageSize, ct);
        return Ok(result);
    }

    private Guid GetAdminId() =>
        Guid.Parse(User.FindFirst("sub")!.Value);
}
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       └── UserManagementController.cs      (new)
        ├── PropelIQ.Application/
        │   ├── Interfaces/
        │   │   └── IUserManagementService.cs        (new)
        │   ├── Models/
        │   │   └── UserAdmin/
        │   │       └── UserAdminModels.cs           (new)
        │   ├── Validators/
        │   │   ├── BulkActionValidator.cs           (new)
        │   │   └── RoleAssignmentValidator.cs       (new)
        │   └── Services/
        │       └── UserManagementService.cs         (new)
        └── PropelIQ.Infrastructure/
            └── Persistence/
                └── AppDbContext.cs                   (modify)
```

> Placeholder: Update on execution based on US_061 task_002, US_056 task_001, and US_016 task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/Interfaces/IUserManagementService.cs | Service contract for list, get, bulk action, and activity history |
| CREATE | server/src/PropelIQ.Application/Models/UserAdmin/UserAdminModels.cs | DTOs for queries, list items, detail, bulk actions, results, activity entries |
| CREATE | server/src/PropelIQ.Application/Services/UserManagementService.cs | Paginated search, bulk action with last-admin guard, role validation, audit logging |
| CREATE | server/src/PropelIQ.Application/Validators/BulkActionValidator.cs | FluentValidation for bulk action (max 200 users, role required for assignment) |
| CREATE | server/src/PropelIQ.Application/Validators/RoleAssignmentValidator.cs | Allowed role-user-type mapping dictionary validation |
| CREATE | server/src/PropelIQ.Api/Controllers/UserManagementController.cs | Admin-only REST endpoints at /api/v1/admin/users/ |

## External References

- ASP.NET Core Authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles
- EF Core Pagination: https://learn.microsoft.com/en-us/ef/core/querying/pagination
- FluentValidation Conditional Rules: https://docs.fluentvalidation.net/en/latest/conditions.html
- ASP.NET Core Identity UserManager: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity

## Build Commands

```bash
# Build backend
cd server
dotnet build

# Run backend
dotnet run --project src/PropelIQ.Api

# Verify endpoints:
# GET  /api/v1/admin/users?searchTerm=john&page=1&pageSize=25
# GET  /api/v1/admin/users/{userId}
# POST /api/v1/admin/users/bulk
#   Body: { "userIds": ["..."], "action": "Deactivate" }
# GET  /api/v1/admin/users/{userId}/activity?page=1&pageSize=25
```

## Implementation Validation Strategy

- [ ] User list endpoint returns paginated results with name, email, role, status, last active (AC-1)
- [ ] Search by name/email filters results correctly (AC-1)
- [ ] Bulk activate/deactivate applies to all selected users and writes audit records (AC-2)
- [ ] Activity history returns login events, role changes, actions in reverse chronological order (AC-3)
- [ ] Bulk deactivation returns summary with success count and failure details (AC-4)
- [ ] Last-admin guard blocks deactivation of all admin accounts (edge case 1)
- [ ] Role assignment validates against allowed role-user-type mappings (edge case 2)
- [ ] Bulk action limited to 200 users per request

## Implementation Checklist

- [ ] Define IUserManagementService contract with ListAsync, GetByIdAsync, BulkActionAsync, GetActivityHistoryAsync
- [ ] Create DTOs for user list query, list item, detail, bulk action request/result, activity entry
- [ ] Implement paginated user listing with name/email search and role/status filters
- [ ] Implement bulk action processing with per-user success/failure tracking and audit logging
- [ ] Implement last-admin guard that blocks deactivation when it would leave zero active admins
- [ ] Implement role-user-type mapping validation for AssignRole bulk action
- [ ] Implement activity history query with reverse chronological ordering and pagination
- [ ] Create UserManagementController with Admin-authorized endpoints (list, get, bulk, activity)

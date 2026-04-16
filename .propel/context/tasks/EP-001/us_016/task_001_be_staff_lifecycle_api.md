# Task - TASK_001

## Requirement Reference

- User Story: us_016
- Story Location: .propel/context/tasks/EP-001/us_016/us_016.md
- Acceptance Criteria:
  - AC-1: Given I am authenticated as an Admin, When I submit a staff invitation with name, email, and role, Then an invitation email is sent to the specified address and a pending staff account is created with a 48-hour invitation expiry.
  - AC-2: Given a staff member receives an invitation email, When they click the invitation link and complete account setup (password), Then their account is activated with the assigned role and the activation event is recorded in the audit log.
  - AC-3: Given I am authenticated as an Admin, When I deactivate a staff account, Then all active sessions for that user are invalidated immediately and the account status is updated to inactive.
  - AC-4: Given a staff invitation link has expired (after 48 hours), When the invitee attempts to use it, Then the system displays "Invitation expired" and offers the Admin the option to resend.
- Edge Cases:
  - What happens if an Admin accidentally deactivates their own account? System prevents self-deactivation with a validation error: "Cannot deactivate your own account."
  - How does the system handle duplicate invitations to the same email? Second invitation resends the email and extends the expiry; duplicate accounts are not created.

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
| Library | ASP.NET Core Identity | 8.x (bundled) |
| Library | FluentValidation | latest stable |
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

Implement the backend API for staff account invitation, activation, and deactivation lifecycle managed by Admin users. The invitation flow creates a pending `ApplicationUser` with a 48-hour token generated via `UserManager.GenerateEmailConfirmationTokenAsync`, sends the invitation email through `INotificationSender`, and stores the invitation metadata (invited-by, role, expiry). The activation endpoint validates the token, sets the password via `UserManager.AddPasswordAsync`, assigns the role, and records an audit event per NFR-010. The deactivation endpoint prevents self-deactivation (Edge Case 1), revokes all refresh tokens for the target user to invalidate active sessions immediately (AC-3), and updates account status. Duplicate invitation handling (Edge Case 2) detects existing pending accounts by email and extends the expiry instead of creating duplicates. Rate limiting per NFR-012 protects invitation and activation endpoints.

## Dependent Tasks

- US_015 task_001 (requires RBAC policies with AdminOnly authorization)
- US_014 task_001 (requires JWT authentication and RefreshToken entity for session invalidation)
- US_013 task_001 (requires ApplicationUser entity and INotificationSender interface)
- US_010 tasks (requires audit log infrastructure)

## Impacted Components

- New: `server/src/PropelIQ.Api/Controllers/StaffManagementController.cs` (invitation, activation, deactivation endpoints)
- New: `server/src/PropelIQ.Api/Models/DTOs/InviteStaffRequest.cs` (invitation request DTO)
- New: `server/src/PropelIQ.Api/Models/DTOs/InviteStaffResponse.cs` (invitation response DTO)
- New: `server/src/PropelIQ.Api/Models/DTOs/ActivateStaffRequest.cs` (activation request DTO)
- New: `server/src/PropelIQ.Api/Models/DTOs/StaffListResponse.cs` (paginated staff list DTO)
- New: `server/src/PropelIQ.Api/Validators/InviteStaffRequestValidator.cs` (FluentValidation)
- New: `server/src/PropelIQ.Api/Validators/ActivateStaffRequestValidator.cs` (FluentValidation)
- Modify: `server/src/PropelIQ.Api/Models/Domain/ApplicationUser.cs` (add InvitedBy, InvitedAt, InvitationExpiresAt, DeactivatedAt, DeactivatedBy fields)
- Modify: `server/src/PropelIQ.Api/Data/AppDbContext.cs` (add index on Email + Status for duplicate detection)

## Implementation Plan

1. **Extend `ApplicationUser` entity** with invitation and deactivation lifecycle fields:

```csharp
// Add to server/src/PropelIQ.Api/Models/Domain/ApplicationUser.cs
public Guid? InvitedBy { get; set; }
public DateTime? InvitedAt { get; set; }
public DateTime? InvitationExpiresAt { get; set; }
public DateTime? ActivatedAt { get; set; }
public DateTime? DeactivatedAt { get; set; }
public Guid? DeactivatedBy { get; set; }
public string AccountStatus { get; set; } = "Pending";
// Allowed values: Pending, Active, Inactive
```

2. **Create the DTOs** for invitation, activation, and staff listing:

```csharp
// server/src/PropelIQ.Api/Models/DTOs/InviteStaffRequest.cs
public record InviteStaffRequest(
    string FullName,
    string Email,
    string Role);

// server/src/PropelIQ.Api/Models/DTOs/InviteStaffResponse.cs
public record InviteStaffResponse(
    Guid UserId,
    string Email,
    string Status,
    DateTime InvitationExpiresAt);

// server/src/PropelIQ.Api/Models/DTOs/ActivateStaffRequest.cs
public record ActivateStaffRequest(
    string Token,
    string Email,
    string Password);

// server/src/PropelIQ.Api/Models/DTOs/StaffListResponse.cs
public record StaffListResponse(
    List<StaffListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record StaffListItem(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string AccountStatus,
    DateTime? LastActive,
    DateTime? InvitedAt,
    DateTime? ActivatedAt);
```

3. **Create FluentValidation validators**:

```csharp
// server/src/PropelIQ.Api/Validators/InviteStaffRequestValidator.cs
public class InviteStaffRequestValidator
    : AbstractValidator<InviteStaffRequest>
{
    private static readonly string[] AllowedRoles =
        ["Staff", "Clinician", "Admin"];

    public InviteStaffRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage("Role must be one of: Staff, Clinician, Admin");
    }
}

// server/src/PropelIQ.Api/Validators/ActivateStaffRequestValidator.cs
public class ActivateStaffRequestValidator
    : AbstractValidator<ActivateStaffRequest>
{
    public ActivateStaffRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12)
            .Matches("[A-Z]").WithMessage("Must contain uppercase letter")
            .Matches("[a-z]").WithMessage("Must contain lowercase letter")
            .Matches("[0-9]").WithMessage("Must contain digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Must contain special char");
    }
}
```

4. **Create `StaffManagementController`** with Admin-only authorization:

```csharp
// server/src/PropelIQ.Api/Controllers/StaffManagementController.cs
[ApiController]
[Route("api/v1/admin/staff")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class StaffManagementController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<StaffManagementController> _logger;
    private readonly RefreshTokenRepository _refreshTokenRepo;

    // Constructor with DI injection

    // POST /api/v1/admin/staff/invite
    [HttpPost("invite")]
    public async Task<IActionResult> InviteStaff(
        [FromBody] InviteStaffRequest request)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Edge Case 2: Check for existing pending account
        var existingUser = await _userManager
            .FindByEmailAsync(request.Email);

        if (existingUser is not null
            && existingUser.AccountStatus == "Active")
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "User Already Active",
                Detail = "A user with this email is already active."
            });
        }

        ApplicationUser user;
        if (existingUser is not null
            && existingUser.AccountStatus == "Pending")
        {
            // Extend expiry, resend email
            user = existingUser;
            user.InvitationExpiresAt = DateTime.UtcNow.AddHours(48);
            user.InvitedBy = Guid.Parse(adminId);
            user.InvitedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }
        else
        {
            // Create new pending account
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                AccountStatus = "Pending",
                InvitedBy = Guid.Parse(adminId),
                InvitedAt = DateTime.UtcNow,
                InvitationExpiresAt = DateTime.UtcNow.AddHours(48),
                EmailConfirmed = false,
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Invitation Failed",
                    Detail = string.Join("; ",
                        result.Errors.Select(e => e.Description))
                });

            await _userManager.AddToRoleAsync(user, request.Role);
        }

        // Generate invitation token (48h lifespan)
        var token = await _userManager
            .GenerateEmailConfirmationTokenAsync(user);

        // Send invitation email
        var inviteLink =
            $"{Request.Scheme}://{Request.Host}/auth/activate"
            + $"?email={Uri.EscapeDataString(user.Email!)}"
            + $"&token={Uri.EscapeDataString(token)}";

        await _notificationSender.SendAsync(
            user.Email!,
            "You've been invited to PropelIQ",
            $"Click here to activate your account: {inviteLink}");

        _logger.LogInformation(
            "Staff invitation sent: UserId={UserId}, Email={Email}, "
            + "InvitedBy={AdminId}, ExpiresAt={ExpiresAt}",
            user.Id, user.Email, adminId,
            user.InvitationExpiresAt);

        return Ok(new InviteStaffResponse(
            user.Id, user.Email!,
            user.AccountStatus,
            user.InvitationExpiresAt!.Value));
    }
}
```

5. **Implement the activation endpoint** validating the token and setting password:

```csharp
// POST /api/v1/admin/staff/activate
[HttpPost("activate")]
[AllowAnonymous]
public async Task<IActionResult> ActivateStaff(
    [FromBody] ActivateStaffRequest request)
{
    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user is null || user.AccountStatus != "Pending")
        return BadRequest(new ProblemDetails
        {
            Status = 400, Title = "Invalid Invitation",
            Detail = "No pending invitation found for this email."
        });

    // AC-4: Check token expiry
    if (user.InvitationExpiresAt < DateTime.UtcNow)
        return BadRequest(new ProblemDetails
        {
            Status = 400, Title = "Invitation Expired",
            Detail = "This invitation has expired. "
                + "Please ask your administrator to resend."
        });

    // Validate email confirmation token
    var confirmResult = await _userManager
        .ConfirmEmailAsync(user, request.Token);
    if (!confirmResult.Succeeded)
        return BadRequest(new ProblemDetails
        {
            Status = 400, Title = "Invalid Token",
            Detail = "The invitation link is invalid or expired."
        });

    // Set password
    var passwordResult = await _userManager
        .AddPasswordAsync(user, request.Password);
    if (!passwordResult.Succeeded)
        return BadRequest(new ProblemDetails
        {
            Status = 400, Title = "Password Error",
            Detail = string.Join("; ",
                passwordResult.Errors.Select(e => e.Description))
        });

    // Activate account
    user.AccountStatus = "Active";
    user.ActivatedAt = DateTime.UtcNow;
    await _userManager.UpdateAsync(user);

    _logger.LogInformation(
        "Staff account activated: UserId={UserId}, Email={Email}",
        user.Id, user.Email);

    return Ok(new { Message = "Account activated successfully." });
}
```

6. **Implement the deactivation endpoint** with self-deactivation guard and session invalidation:

```csharp
// POST /api/v1/admin/staff/{userId}/deactivate
[HttpPost("{userId:guid}/deactivate")]
public async Task<IActionResult> DeactivateStaff(Guid userId)
{
    var adminId = Guid.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // Edge Case 1: Prevent self-deactivation
    if (userId == adminId)
        return BadRequest(new ProblemDetails
        {
            Status = 400,
            Title = "Self-Deactivation Denied",
            Detail = "Cannot deactivate your own account."
        });

    var user = await _userManager.FindByIdAsync(userId.ToString());
    if (user is null)
        return NotFound();

    if (user.AccountStatus == "Inactive")
        return Conflict(new ProblemDetails
        {
            Status = 409, Title = "Already Inactive",
            Detail = "This account is already deactivated."
        });

    // Deactivate
    user.AccountStatus = "Inactive";
    user.DeactivatedAt = DateTime.UtcNow;
    user.DeactivatedBy = adminId;
    await _userManager.UpdateAsync(user);

    // AC-3: Invalidate all active sessions
    // Revoke all refresh tokens for the user
    await _refreshTokenRepo.RevokeAllForUserAsync(
        userId, "Account deactivated by admin");

    // Update security stamp to invalidate existing JWT validation
    await _userManager.UpdateSecurityStampAsync(user);

    _logger.LogInformation(
        "Staff account deactivated: UserId={UserId}, "
        + "DeactivatedBy={AdminId}",
        userId, adminId);

    return Ok(new { Message = "Account deactivated successfully." });
}
```

7. **Implement the staff listing endpoint** with pagination, filtering by status, and search:

```csharp
// GET /api/v1/admin/staff?page=1&pageSize=25&status=Active&search=john
[HttpGet]
public async Task<IActionResult> GetStaffList(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 25,
    [FromQuery] string? status = null,
    [FromQuery] string? search = null)
{
    var query = _userManager.Users.AsQueryable();

    if (!string.IsNullOrWhiteSpace(status))
        query = query.Where(u => u.AccountStatus == status);

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(u =>
            u.FullName.Contains(search)
            || u.Email!.Contains(search));

    var totalCount = await query.CountAsync();
    var items = await query
        .OrderBy(u => u.FullName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => new StaffListItem(
            u.Id, u.FullName, u.Email!,
            /* role resolved from UserRoles join */
            string.Empty, u.AccountStatus,
            u.LastLoginAt, u.InvitedAt, u.ActivatedAt))
        .ToListAsync();

    return Ok(new StaffListResponse(
        items, totalCount, page, pageSize));
}
```

8. **Configure `DataProtectionTokenProviderOptions`** for 48-hour invitation token lifespan and add rate limiting for invitation endpoints per NFR-012:

```csharp
// In Program.cs — configure token lifespan for invitations
builder.Services.Configure<DataProtectionTokenProviderOptions>(
    options => options.TokenLifespan = TimeSpan.FromHours(48));

// Rate limiting for invitation endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("invitation", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(15);
    });
});
```

## Current Project State

```text
propelIQ/
├── server/
│   └── src/
│       └── PropelIQ.Api/
│           ├── Program.cs                     (from US_001)
│           ├── Controllers/
│           │   ├── AuthController.cs          (from US_014 task_001)
│           │   └── StaffManagementController.cs  (NEW)
│           ├── Authorization/
│           │   └── Policies/
│           │       └── AuthorizationPolicies.cs  (from US_015 task_001)
│           ├── Models/
│           │   ├── Domain/
│           │   │   └── ApplicationUser.cs     (from US_013, modified)
│           │   └── DTOs/
│           ├── Data/
│           │   └── AppDbContext.cs             (from US_009)
│           ├── Validators/
│           └── Infrastructure/
│               └── Telemetry/                 (from US_007)
└── client/                                    (from US_001)
```

> Placeholder: Update on execution based on dependent task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Api/Controllers/StaffManagementController.cs | Admin-only controller with invite, activate, deactivate, list endpoints |
| CREATE | server/src/PropelIQ.Api/Models/DTOs/InviteStaffRequest.cs | DTO: FullName, Email, Role |
| CREATE | server/src/PropelIQ.Api/Models/DTOs/InviteStaffResponse.cs | DTO: UserId, Email, Status, InvitationExpiresAt |
| CREATE | server/src/PropelIQ.Api/Models/DTOs/ActivateStaffRequest.cs | DTO: Token, Email, Password |
| CREATE | server/src/PropelIQ.Api/Models/DTOs/StaffListResponse.cs | Paginated list DTO with StaffListItem records |
| CREATE | server/src/PropelIQ.Api/Validators/InviteStaffRequestValidator.cs | FluentValidation for name, email, allowed roles |
| CREATE | server/src/PropelIQ.Api/Validators/ActivateStaffRequestValidator.cs | FluentValidation for token, email, 12-char password strength |
| MODIFY | server/src/PropelIQ.Api/Models/Domain/ApplicationUser.cs | Add InvitedBy, InvitedAt, InvitationExpiresAt, ActivatedAt, DeactivatedAt, DeactivatedBy, AccountStatus |
| MODIFY | server/src/PropelIQ.Api/Data/AppDbContext.cs | Add index on Email + AccountStatus for duplicate detection |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Configure 48h token lifespan and invitation rate limiter |

## External References

- ASP.NET Core Identity account confirmation: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-8.0
- UserManager token generation: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.generateemailconfirmationtokenasync
- DataProtectionTokenProviderOptions: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.dataprotectiontokenprovideroptions
- ASP.NET Core rate limiting: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0
- FluentValidation docs: https://docs.fluentvalidation.net/en/latest/
- OWASP session management: https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend
dotnet run

# Run tests
cd server/tests/PropelIQ.Api.Tests
dotnet test
```

## Implementation Validation Strategy

- [ ] Admin can invite staff with name, email, role and receives 200 with pending user (AC-1)
- [ ] Invitation email is sent with activation link containing token (AC-1)
- [ ] Pending account has 48-hour InvitationExpiresAt set (AC-1)
- [ ] Invitee activates account with valid token and password, status becomes Active, audit logged (AC-2)
- [ ] Admin deactivates staff account, all refresh tokens revoked, security stamp updated (AC-3)
- [ ] Expired invitation returns 400 with "Invitation expired" message (AC-4)
- [ ] Self-deactivation returns 400 "Cannot deactivate your own account" (Edge-1)
- [ ] Duplicate invitation to same email extends expiry and resends email without creating duplicate (Edge-2)
- [ ] Rate limiter enforces max 10 invitations per 15 minutes per NFR-012

## Implementation Checklist

- [ ] Extend `ApplicationUser` with InvitedBy, InvitedAt, InvitationExpiresAt, ActivatedAt, DeactivatedAt, DeactivatedBy, AccountStatus fields
- [ ] Create DTOs: InviteStaffRequest, InviteStaffResponse, ActivateStaffRequest, StaffListResponse with StaffListItem
- [ ] Create FluentValidation validators for InviteStaffRequest (email, name, allowed roles) and ActivateStaffRequest (token, email, 12-char password)
- [ ] Implement POST /invite endpoint with duplicate detection (Edge-2), token generation, and email sending (AC-1)
- [ ] Implement POST /activate endpoint with token validation, expiry check (AC-4), password setting, role assignment, and audit logging (AC-2)
- [ ] Implement POST /{userId}/deactivate endpoint with self-deactivation guard (Edge-1), refresh token revocation, and security stamp update (AC-3)
- [ ] Implement GET staff listing with pagination, status filter, and search
- [ ] Configure 48-hour DataProtectionTokenProviderOptions and invitation rate limiter in Program.cs

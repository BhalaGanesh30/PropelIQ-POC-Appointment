using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PropelIQ.Api.Authorization.Policies;
using PropelIQ.Api.Models.DTOs;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Admin-only endpoints for staff account invitation, activation, deactivation, and listing.
///
/// Invitation flow (AC-1):
///   Admin POSTs invite → pending account created → DataProtection token sent via email → 48h expiry.
///
/// Activation flow (AC-2):
///   Invitee POSTs token + password → account activated → audit event recorded.
///
/// Deactivation flow (AC-3):
///   Admin POSTs deactivate → refresh tokens revoked → security stamp rotated → account Inactive.
///
/// Duplicate detection (Edge-2):
///   Existing Pending account for same email → expiry extended + email resent; no duplicate created.
///
/// Self-deactivation guard (Edge-1):
///   Admin cannot deactivate their own account.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class StaffManagementController : BaseApiController
{
    // Custom token purpose — kept separate from the OTP email-confirmation flow
    // so invitations use the DataProtection provider (opaque URL-safe tokens, 48h).
    private const string InvitationTokenPurpose = "StaffInvitation";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly INotificationSender _notifications;
    private readonly AppDbContext _db;
    private readonly ILogger<StaffManagementController> _logger;

    public StaffManagementController(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokens,
        INotificationSender notifications,
        AppDbContext db,
        ILogger<StaffManagementController> logger)
    {
        _userManager = userManager;
        _refreshTokens = refreshTokens;
        _notifications = notifications;
        _db = db;
        _logger = logger;
    }

    // ── Invite ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Invite a new staff member.
    /// Creates a Pending account (or extends expiry on an existing Pending account)
    /// and emails an activation link with a 48-hour DataProtection token (AC-1).
    ///
    /// Duplicate email with Active status → 409 Conflict.
    /// Duplicate email with Pending status → expiry extended, email resent (Edge-2).
    /// </summary>
    [HttpPost("invite")]
    [EnableRateLimiting("invite-policy")]
    [ProducesResponseType(typeof(InviteStaffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteStaff(
        [FromBody] InviteStaffRequest request,
        CancellationToken ct)
    {
        var adminId = TryGetCurrentUserId() ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        var existing = await _userManager.FindByEmailAsync(request.Email);

        // Active account — cannot re-invite.
        if (existing is not null && existing.AccountStatus == "Active")
            return Problem(
                title: "User Already Active",
                statusCode: StatusCodes.Status409Conflict,
                detail: "A user with this email already has an active account.");

        ApplicationUser user;

        if (existing is { AccountStatus: "Pending" })
        {
            // Edge-2: extend expiry and resend instead of creating a duplicate.
            user = existing;
            user.InvitedBy = adminId;
            user.InvitedAt = now;
            user.InvitationExpiresAt = now.AddHours(48);
            await _userManager.UpdateAsync(user);
        }
        else
        {
            // New invitation — create a passwordless Pending account.
            // Split FullName on the first space; remaining words become LastName.
            var nameParts = request.FullName.Trim().Split(' ', 2);
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = nameParts[0],
                LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
                AccountStatus = "Pending",
                IsActive = false,
                EmailConfirmed = false,
                InvitedBy = adminId,
                InvitedAt = now,
                InvitationExpiresAt = now.AddHours(48),
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return Problem(
                    title: "Invitation Failed",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: string.Join("; ", createResult.Errors.Select(e => e.Description)));

            // Assign the requested role immediately — it is visible before activation.
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        // Generate a DataProtection invitation token (48h lifespan via
        // DataProtectionTokenProviderOptions configured in Program.cs).
        var token = await _userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultProvider, InvitationTokenPurpose);

        var inviteLink =
            $"{Request.Scheme}://{Request.Host}/auth/activate" +
            $"?email={Uri.EscapeDataString(user.Email!)}" +
            $"&token={Uri.EscapeDataString(token)}";

        // DEV: log the link so developers can activate without an SMTP server.
        _logger.LogWarning(
            "\n======================================================" +
            "\n[DEV] Staff invitation link for {Email}:" +
            "\n{Link}" +
            "\n======================================================",
            user.Email, inviteLink);

        await _notifications.SendEmailAsync(
            user.Email!,
            "You've been invited to PropelIQ",
            $"<p>You have been invited to join PropelIQ as <strong>{request.Role}</strong>.</p>" +
            $"<p><a href='{inviteLink}'>Click here to activate your account</a></p>" +
            $"<p>This link expires in 48 hours.</p>",
            ct);

        _logger.LogInformation(
            "Staff invitation sent: UserId={UserId} Email={Email} Role={Role} InvitedBy={AdminId} ExpiresAt={ExpiresAt}",
            user.Id, user.Email, request.Role, adminId, user.InvitationExpiresAt);

        await WriteAuditAsync(
            "staff.invited", adminId, user.Id,
            nameof(ApplicationUser),
            $"Staff invitation sent to {user.Email} for role {request.Role}",
            ct);

        return Ok(new InviteStaffResponse(
            user.Id, user.Email!, user.AccountStatus, user.InvitationExpiresAt!.Value));
    }

    // ── Activate ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Activate an invited staff account.
    /// Validates the DataProtection invitation token, enforces 48-hour expiry (AC-4),
    /// sets the initial password, marks the account Active, and records an audit event (AC-2).
    ///
    /// This endpoint is anonymous — the invitee has no credentials yet.
    /// </summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateStaff(
        [FromBody] ActivateStaffRequest request,
        CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || user.AccountStatus != "Pending")
            return Problem(
                title: "Invalid Invitation",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "No pending invitation found for this email.");

        // AC-4: enforce the 48-hour invitation expiry stored on the entity.
        if (user.InvitationExpiresAt < DateTimeOffset.UtcNow)
            return Problem(
                title: "Invitation Expired",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "This invitation has expired. Please ask your administrator to resend.");

        // Validate the DataProtection invitation token.
        var isValidToken = await _userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultProvider, InvitationTokenPurpose, request.Token);

        if (!isValidToken)
            return Problem(
                title: "Invalid Token",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The invitation link is invalid or has already been used.");

        // Set the initial password.
        var passwordResult = await _userManager.AddPasswordAsync(user, request.Password);
        if (!passwordResult.Succeeded)
            return Problem(
                title: "Password Error",
                statusCode: StatusCodes.Status400BadRequest,
                detail: string.Join("; ", passwordResult.Errors.Select(e => e.Description)));

        // Activate the account.
        user.AccountStatus = "Active";
        user.IsActive = true;
        user.EmailConfirmed = true;
        user.ActivatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation(
            "Staff account activated: UserId={UserId} Email={Email}",
            user.Id, user.Email);

        await WriteAuditAsync(
            "staff.activated", user.Id, user.Id,
            nameof(ApplicationUser),
            $"Staff account activated: {user.Email}",
            ct);

        return Ok(new { message = "Account activated successfully. You can now log in." });
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    /// <summary>
    /// Deactivate a staff account.
    /// Prevents self-deactivation (Edge-1), revokes all active refresh tokens to invalidate
    /// sessions immediately (AC-3), and rotates the security stamp so existing JWTs fail
    /// validation on the next request.
    /// </summary>
    [HttpPost("{userId:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateStaff(
        Guid userId,
        CancellationToken ct)
    {
        var adminId = TryGetCurrentUserId() ?? Guid.Empty;

        // Edge-1: prevent self-deactivation.
        if (userId == adminId)
            return Problem(
                title: "Self-Deactivation Denied",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Cannot deactivate your own account.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Problem(
                title: "User Not Found",
                statusCode: StatusCodes.Status404NotFound);

        if (user.AccountStatus == "Inactive")
            return Problem(
                title: "Already Inactive",
                statusCode: StatusCodes.Status409Conflict,
                detail: "This account is already deactivated.");

        // Mark account as Inactive.
        user.AccountStatus = "Inactive";
        user.IsActive = false;
        user.DeactivatedAt = DateTimeOffset.UtcNow;
        user.DeactivatedBy = adminId;
        await _userManager.UpdateAsync(user);

        // AC-3: revoke all active refresh tokens — invalidates any active session immediately.
        await _refreshTokens.RevokeAllForUserAsync(
            userId, "Account deactivated by admin", ct);

        // Rotate security stamp — any existing JWT that validates the stamp claim
        // will fail on the next request after the token is cached locally.
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation(
            "Staff account deactivated: UserId={UserId} DeactivatedBy={AdminId}",
            userId, adminId);

        await WriteAuditAsync(
            "staff.deactivated", adminId, userId,
            nameof(ApplicationUser),
            $"Staff account deactivated: {user.Email}",
            ct);

        return Ok(new { message = "Account deactivated and all active sessions invalidated." });
    }

    // ── List staff ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated, filterable list of staff accounts.
    /// Supports optional status filter (Active, Pending, Inactive) and
    /// free-text search across name and email.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(StaffListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaffList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // Clamp page size to prevent accidental full-table scans.
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(u => u.AccountStatus == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).Contains(search)
                || u.Email!.Contains(search));

        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Resolve roles per user (Identity stores these in a separate join table).
        var items = new List<StaffListItem>(users.Count);
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(new StaffListItem(
                u.Id,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.Email ?? string.Empty,
                roles.FirstOrDefault() ?? string.Empty,
                u.AccountStatus,
                u.InvitedAt,
                u.ActivatedAt,
                u.DeactivatedAt));
        }

        return Ok(new StaffListResponse(items, totalCount, page, pageSize));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task WriteAuditAsync(
        string eventType,
        Guid actorUserId,
        Guid? targetEntityId,
        string targetEntityType,
        string description,
        CancellationToken ct)
    {
        try
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                EventType = eventType,
                ActorUserId = actorUserId,
                TargetEntityId = targetEntityId,
                TargetEntityType = targetEntityType,
                OccurredAt = DateTimeOffset.UtcNow,
                Details = new AuditDetails
                {
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ChangeDescription = description
                }
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit logging must never fail the caller's request.
            _logger.LogError(ex, "Failed to write audit record for event {EventType}", eventType);
        }
    }
}

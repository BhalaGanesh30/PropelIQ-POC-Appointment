using Microsoft.AspNetCore.Identity;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user for authentication and account management.
/// Stored in the <c>auth</c> schema; separate from the domain <c>User</c>
/// entity in the <c>app</c> schema to preserve clean modular boundaries.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── Staff invitation lifecycle (us_016) ──────────────────────────────────

    /// <summary>Account lifecycle state: Active, Pending, or Inactive.</summary>
    public string AccountStatus { get; set; } = "Active";

    /// <summary>ID of the Admin who sent the invitation.</summary>
    public Guid? InvitedBy { get; set; }

    /// <summary>UTC timestamp when the invitation was originally sent.</summary>
    public DateTimeOffset? InvitedAt { get; set; }

    /// <summary>UTC timestamp after which the invitation token is no longer valid (48 hours).</summary>
    public DateTimeOffset? InvitationExpiresAt { get; set; }

    /// <summary>UTC timestamp when the staff member completed account activation.</summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>UTC timestamp when the account was deactivated by an Admin.</summary>
    public DateTimeOffset? DeactivatedAt { get; set; }

    /// <summary>ID of the Admin who deactivated the account.</summary>
    public Guid? DeactivatedBy { get; set; }
}

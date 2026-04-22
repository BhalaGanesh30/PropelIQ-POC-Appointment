namespace PropelIQ.Api.Models.DTOs;

/// <summary>
/// Response for a successful staff invitation.
/// </summary>
public sealed record InviteStaffResponse(
    Guid UserId,
    string Email,
    string AccountStatus,
    DateTimeOffset InvitationExpiresAt);

namespace PropelIQ.Api.Models.DTOs;

/// <summary>
/// Payload for POST /api/v1/admin/staff/invite (Admin only).
/// Validated by <see cref="Validators.InviteStaffRequestValidator"/>.
/// </summary>
public sealed record InviteStaffRequest(
    string FullName,
    string Email,
    string Role);

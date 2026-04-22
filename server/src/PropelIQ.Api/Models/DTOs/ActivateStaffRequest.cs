namespace PropelIQ.Api.Models.DTOs;

/// <summary>
/// Payload for POST /api/v1/admin/staff/activate (anonymous — invitee completes setup).
/// Validated by <see cref="Validators.ActivateStaffRequestValidator"/>.
/// </summary>
public sealed record ActivateStaffRequest(
    string Token,
    string Email,
    string Password);

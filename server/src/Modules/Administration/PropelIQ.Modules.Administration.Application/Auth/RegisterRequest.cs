namespace PropelIQ.Modules.Administration.Application.Auth;

/// <summary>
/// Payload for the POST /api/v1/auth/register endpoint.
/// Validated by <see cref="Validators.RegisterRequestValidator"/> via FluentValidation.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber);

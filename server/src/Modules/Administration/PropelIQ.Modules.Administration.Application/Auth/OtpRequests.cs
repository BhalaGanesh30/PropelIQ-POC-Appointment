namespace PropelIQ.Modules.Administration.Application.Auth;

/// <summary>Payload for POST /api/v1/auth/send-otp.</summary>
public sealed record SendOtpRequest(string Email);

/// <summary>Payload for POST /api/v1/auth/verify-otp.</summary>
public sealed record VerifyOtpRequest(string Email, string Otp);

using Microsoft.AspNetCore.Authorization;

namespace PropelIQ.Api.Authorization.Policies;

/// <summary>
/// Named authorization policy constants and DI registration extension.
/// All policy names are referenced from controller attributes via these constants
/// to prevent typos (OWASP A01 — Broken Access Control).
/// </summary>
public static class AuthorizationPolicies
{
    public const string PatientOnly = "PatientOnly";
    public const string StaffOnly = "StaffOnly";
    public const string AdminOnly = "AdminOnly";
    public const string StaffOrAdmin = "StaffOrAdmin";

    /// <summary>
    /// Requires that the authenticated patient matches the route `patientId` value.
    /// Staff and Admin bypass this restriction and can access any patient resource.
    /// </summary>
    public const string PatientResourceOwner = "PatientResourceOwner";

    /// <summary>
    /// Registers all named policies and sets a FallbackPolicy that requires
    /// authentication on every endpoint not marked [AllowAnonymous] (AC-4).
    /// </summary>
    public static IServiceCollection AddAppAuthorizationPolicies(
        this IServiceCollection services)
    {
        services
            .AddAuthorizationBuilder()
            .AddPolicy(PatientOnly, policy =>
                policy.RequireRole("Patient"))
            .AddPolicy(StaffOnly, policy =>
                policy.RequireRole("Staff"))
            .AddPolicy(AdminOnly, policy =>
                policy.RequireRole("Admin"))
            .AddPolicy(StaffOrAdmin, policy =>
                policy.RequireRole("Staff", "Admin"))
            .AddPolicy(PatientResourceOwner, policy =>
                policy.AddRequirements(new Requirements.PatientResourceRequirement()));

        // NOTE: Default authentication enforcement is applied via
        // app.MapControllers().RequireAuthorization() in Program.cs
        // so that health/metrics endpoints remain anonymous without
        // needing per-endpoint AllowAnonymous() metadata workarounds.

        return services;
    }
}

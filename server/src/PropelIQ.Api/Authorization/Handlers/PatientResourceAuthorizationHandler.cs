using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PropelIQ.Api.Authorization.Requirements;

namespace PropelIQ.Api.Authorization.Handlers;

/// <summary>
/// Enforces patient-scoped data access (AC-2).
/// Rules:
///   - Admin and Staff may access any patient resource.
///   - Patient may only access resources where the route `patientId` matches
///     their own `patient_id` JWT claim.
/// Failure to satisfy the requirement results in HTTP 403 (framework default).
/// </summary>
public sealed class PatientResourceAuthorizationHandler
    : AuthorizationHandler<PatientResourceRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PatientResourceAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PatientResourceRequirement requirement)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

        // Staff and Admin bypass patient-scoping (AC-2: only patients are restricted).
        if (role is "Admin" or "Staff")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Patients may only access their own resource.
        var httpContext = _httpContextAccessor.HttpContext;
        var routePatientId = httpContext?.GetRouteValue("patientId")?.ToString();
        var userPatientId = context.User.FindFirst("patient_id")?.Value;

        if (!string.IsNullOrEmpty(routePatientId)
            && !string.IsNullOrEmpty(userPatientId)
            && string.Equals(routePatientId, userPatientId, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        // If not succeeded, the framework will return 403 via ForbiddenResultHandler.
        return Task.CompletedTask;
    }
}

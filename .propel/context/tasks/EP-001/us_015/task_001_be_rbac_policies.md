# Task - TASK_001

## Requirement Reference

- User Story: us_015
- Story Location: .propel/context/tasks/EP-001/us_015/us_015.md
- Acceptance Criteria:
  - AC-1: Given a Patient user is authenticated, When they attempt to access a staff-only endpoint (e.g., queue management), Then the API returns HTTP 403 Forbidden and the access attempt is recorded in the audit log.
  - AC-2: Given a Staff user is authenticated, When they request a resource owned by a different patient, Then the API enforces patient-scoped data access and returns only data belonging to the authorized scope.
  - AC-3: Given an Admin user is authenticated, When they access the user management API, Then the full admin policy scope is granted and the access event is audited.
  - AC-4: Given a JWT token contains a role claim, When the API validates the token, Then the role claim is extracted and authorization policies are applied before the request reaches the controller action.
- Edge Cases:
  - What happens if a user's role is changed while they have an active session? Role change takes effect on next token refresh; the current token remains valid until expiry but new tokens reflect the updated role.

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
| Library | Microsoft.AspNetCore.Authorization | 8.x (bundled) |
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

Implement the ASP.NET Core 8.x policy-based authorization engine for PropelIQ with named policies for Patient, Staff, and Admin roles, a custom `IAuthorizationHandler` for patient-scoped data access (AC-2), a `FallbackPolicy` requiring authentication on all endpoints by default, and audit logging for every access-control decision (AC-1, AC-3). The role claim is extracted from the JWT bearer token validated in US_014, and authorization policies are evaluated before the request reaches the controller action (AC-4). All denied access attempts produce HTTP 403 with an audit trail entry containing user ID, endpoint, role, timestamp, and IP address per NFR-010.

## Dependent Tasks

- US_014 task_001 (requires JWT bearer authentication with role claim in access token)
- US_001 tasks (requires project scaffold and middleware pipeline)
- US_009 tasks (requires domain entity definitions for Patient, User)
- US_010 tasks (requires audit log infrastructure)

## Impacted Components

- New: `server/src/PropelIQ.Api/Authorization/Policies/AuthorizationPolicies.cs` (named policy constants and registration)
- New: `server/src/PropelIQ.Api/Authorization/Requirements/PatientResourceRequirement.cs` (IAuthorizationRequirement)
- New: `server/src/PropelIQ.Api/Authorization/Handlers/PatientResourceAuthorizationHandler.cs` (IAuthorizationHandler for patient-scoped access)
- New: `server/src/PropelIQ.Api/Authorization/Handlers/AuditAuthorizationHandler.cs` (cross-cutting audit handler for all policy evaluations)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register authorization services, policies, FallbackPolicy)
- Modify: `server/src/PropelIQ.Api/Controllers/AuthController.cs` (add [AllowAnonymous] to public endpoints)
- Modify: existing controllers (apply [Authorize(Policy = "...")] attributes per role)

## Implementation Plan

1. **Define authorization policy constants and registration** as a centralized configuration class:

```csharp
// server/src/PropelIQ.Api/Authorization/Policies/AuthorizationPolicies.cs
public static class AuthorizationPolicies
{
    public const string PatientOnly = "PatientOnly";
    public const string StaffOnly = "StaffOnly";
    public const string AdminOnly = "AdminOnly";
    public const string StaffOrAdmin = "StaffOrAdmin";
    public const string PatientResourceOwner = "PatientResourceOwner";

    public static IServiceCollection AddAppAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(PatientOnly, policy =>
                policy.RequireRole("Patient"))
            .AddPolicy(StaffOnly, policy =>
                policy.RequireRole("Staff"))
            .AddPolicy(AdminOnly, policy =>
                policy.RequireRole("Admin"))
            .AddPolicy(StaffOrAdmin, policy =>
                policy.RequireRole("Staff", "Admin"))
            .AddPolicy(PatientResourceOwner, policy =>
                policy.AddRequirements(new PatientResourceRequirement()));

        // FallbackPolicy: require authenticated user on ALL endpoints
        // unless explicitly marked [AllowAnonymous]
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
```

2. **Create the `PatientResourceRequirement` and handler** for patient-scoped data access (AC-2). The handler compares the `patient_id` route value or resource property against the authenticated user's linked patient ID from the JWT `sub` claim:

```csharp
// server/src/PropelIQ.Api/Authorization/Requirements/PatientResourceRequirement.cs
public class PatientResourceRequirement : IAuthorizationRequirement { }

// server/src/PropelIQ.Api/Authorization/Handlers/
//   PatientResourceAuthorizationHandler.cs
public class PatientResourceAuthorizationHandler
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
        var user = context.User;
        var role = user.FindFirst(
            ClaimTypes.Role)?.Value;

        // Admin and Staff can access any patient resource
        if (role is "Admin" or "Staff")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Patient can only access their own resources
        var httpContext = _httpContextAccessor.HttpContext;
        var routePatientId = httpContext?.GetRouteValue("patientId")
            ?.ToString();
        var userPatientId = user.FindFirst("patient_id")?.Value;

        if (!string.IsNullOrEmpty(routePatientId)
            && !string.IsNullOrEmpty(userPatientId)
            && routePatientId == userPatientId)
        {
            context.Succeed(requirement);
        }

        // If requirement is not succeeded, framework returns 403
        return Task.CompletedTask;
    }
}
```

3. **Create the `AuditAuthorizationHandler`** as a cross-cutting handler that logs every authorization decision (success and failure) to satisfy NFR-010 audit requirements (AC-1, AC-3):

```csharp
// server/src/PropelIQ.Api/Authorization/Handlers/
//   AuditAuthorizationHandler.cs
public class AuditAuthorizationHandler : IAuthorizationHandler
{
    private readonly ILogger<AuditAuthorizationHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditAuthorizationHandler(
        ILogger<AuditAuthorizationHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        var endpoint = httpContext?.GetEndpoint()?.DisplayName;
        var ip = httpContext?.Connection.RemoteIpAddress?.ToString();

        if (context.HasFailed || context.PendingRequirements.Any())
        {
            _logger.LogWarning(
                "Authorization DENIED: UserId={UserId}, Role={Role}, "
                + "Endpoint={Endpoint}, IP={IP}, "
                + "PendingRequirements={Requirements}",
                userId, role, endpoint, ip,
                string.Join(", ", context.PendingRequirements
                    .Select(r => r.GetType().Name)));
        }
        else
        {
            _logger.LogInformation(
                "Authorization GRANTED: UserId={UserId}, Role={Role}, "
                + "Endpoint={Endpoint}, IP={IP}",
                userId, role, endpoint, ip);
        }

        return Task.CompletedTask;
    }
}
```

4. **Register all authorization services** in `Program.cs`:

```csharp
// In Program.cs — after AddAuthentication/AddJwtBearer from US_014
builder.Services.AddHttpContextAccessor();
builder.Services.AddAppAuthorizationPolicies();
builder.Services
    .AddScoped<IAuthorizationHandler, PatientResourceAuthorizationHandler>();
builder.Services
    .AddScoped<IAuthorizationHandler, AuditAuthorizationHandler>();

// In the middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
```

5. **Apply `[Authorize]` policy attributes to controllers** per role scope (AC-1, AC-3, AC-4). Mark public auth endpoints with `[AllowAnonymous]`:

```csharp
// AuthController — public endpoints exempt from FallbackPolicy
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase { ... }

// Staff-only controller example (AC-1 — Patient gets 403)
[ApiController]
[Route("api/v1/queue")]
[Authorize(Policy = AuthorizationPolicies.StaffOrAdmin)]
public class QueueController : ControllerBase { ... }

// Admin-only controller (AC-3 — full admin scope + audit)
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class UserManagementController : ControllerBase { ... }

// Patient-scoped resource endpoints (AC-2)
[ApiController]
[Route("api/v1/patients/{patientId}")]
[Authorize(Policy = AuthorizationPolicies.PatientResourceOwner)]
public class PatientController : ControllerBase { ... }
```

6. **Handle 403 Forbidden responses** consistently via a custom middleware or `IAuthorizationMiddlewareResultHandler` that returns a standardized ProblemDetails response:

```csharp
// server/src/PropelIQ.Api/Authorization/ForbiddenResultHandler.cs
public class ForbiddenResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 403,
                Title = "Forbidden",
                Detail = "You do not have permission to access this resource.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
```

Register in DI:
```csharp
builder.Services
    .AddSingleton<IAuthorizationMiddlewareResultHandler,
        ForbiddenResultHandler>();
```

7. **Configure structured audit logging output** so authorization events are captured by the OpenTelemetry pipeline (from US_007) and meet the NFR-010 7-year retention requirement. The `AuditAuthorizationHandler` uses `ILogger` with structured fields, which are automatically exported to Loki/Grafana.

## Current Project State

```text
propelIQ/
├── server/
│   └── src/
│       └── PropelIQ.Api/
│           ├── Program.cs                    (from US_001)
│           ├── Controllers/
│           │   └── AuthController.cs         (from US_014 task_001)
│           ├── Authorization/
│           │   ├── Policies/
│           │   ├── Requirements/
│           │   └── Handlers/
│           ├── Models/
│           │   └── Domain/                   (from US_009)
│           ├── Data/
│           │   └── AppDbContext.cs            (from US_009)
│           └── Infrastructure/
│               └── Telemetry/                (from US_007)
└── client/                                   (from US_001)
```

> Placeholder: Update on execution based on US_001, US_009, US_014 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Api/Authorization/Policies/AuthorizationPolicies.cs | Named policy constants (PatientOnly, StaffOnly, AdminOnly, StaffOrAdmin, PatientResourceOwner) and FallbackPolicy registration |
| CREATE | server/src/PropelIQ.Api/Authorization/Requirements/PatientResourceRequirement.cs | IAuthorizationRequirement marker for patient-scoped resource access |
| CREATE | server/src/PropelIQ.Api/Authorization/Handlers/PatientResourceAuthorizationHandler.cs | Custom handler comparing route patientId against JWT patient_id claim |
| CREATE | server/src/PropelIQ.Api/Authorization/Handlers/AuditAuthorizationHandler.cs | Cross-cutting handler logging all authorization decisions with user, role, endpoint, IP |
| CREATE | server/src/PropelIQ.Api/Authorization/ForbiddenResultHandler.cs | IAuthorizationMiddlewareResultHandler returning standardized ProblemDetails on 403 |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register authorization policies, handlers, FallbackPolicy, ForbiddenResultHandler |
| MODIFY | server/src/PropelIQ.Api/Controllers/AuthController.cs | Add [AllowAnonymous] attribute to public auth endpoints |
| MODIFY | existing controllers | Apply [Authorize(Policy = "...")] attributes per role scope |

## External References

- ASP.NET Core policy-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-8.0
- ASP.NET Core resource-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-8.0
- ASP.NET Core role-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- IAuthorizationMiddlewareResultHandler: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/customizingauthorizationmiddlewareresponse?view=aspnetcore-8.0
- Claims-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/claims?view=aspnetcore-8.0
- OWASP access control cheat sheet: https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html

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

- [ ] Patient calling staff-only endpoint receives HTTP 403 with ProblemDetails body (AC-1)
- [ ] Audit log records denied access with UserId, Role, Endpoint, IP (AC-1)
- [ ] Staff requesting a different patient's resource receives only authorized-scope data (AC-2)
- [ ] Patient requesting their own resource succeeds with HTTP 200 (AC-2)
- [ ] Admin accessing user management API receives full scope response and audit log entry (AC-3)
- [ ] JWT role claim is extracted and policy is evaluated before controller action (AC-4)
- [ ] FallbackPolicy denies unauthenticated requests to all endpoints not marked [AllowAnonymous]
- [ ] Role change on active session: old token still valid until expiry, new token reflects updated role (Edge-1)
- [ ] ProblemDetails response follows RFC 9110 format with status 403

## Implementation Checklist

- [x] Create `AuthorizationPolicies` static class with named policy constants and `AddAppAuthorizationPolicies` extension method; enforcement via `MapControllers().RequireAuthorization()` (FallbackPolicy removed — blocked health/metrics endpoints)
- [x] Create `PatientResourceRequirement` and `PatientResourceAuthorizationHandler` comparing route patientId against JWT patient_id claim
- [x] Create `AuditAuthorizationHandler` logging all authorization decisions (granted and denied) with structured fields
- [x] Create `ForbiddenResultHandler` returning standardized ProblemDetails on 403
- [x] Register all authorization services, handlers in Program.cs; switched `MapControllers()` to `MapControllers().RequireAuthorization()`
- [x] Apply `[AllowAnonymous]` to public auth endpoints (AuthController, HealthController, DiagnosticsController ping/error)
- [x] Verified structured audit log output integrates with OpenTelemetry pipeline (US_007) — AuditAuthorizationHandler uses ILogger with structured fields exported via OTel

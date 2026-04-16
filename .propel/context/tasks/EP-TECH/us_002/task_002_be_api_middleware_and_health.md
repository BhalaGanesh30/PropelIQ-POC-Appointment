# Task - TASK_002

## Requirement Reference

- User Story: us_002
- Story Location: .propel/context/tasks/EP-TECH/us_002/us_002.md
- Acceptance Criteria:
  - AC-2: Given the API project is running, When a request is sent to `GET /api/v1/health`, Then the response returns HTTP 200 with a JSON health status payload within 500 ms.
  - AC-4: Given the API is running, When an unauthenticated request is sent to a protected endpoint, Then the API returns HTTP 401 with a structured error response, not a raw exception.
- Edge Case:
  - What happens if an unhandled exception occurs in a controller? Global exception middleware returns HTTP 500 with a problem details response, no stack trace exposed externally.
  - How does the system behave if the database connection is unavailable on startup? Health check returns degraded status; application starts in reduced mode and retries on interval.

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
| Database | N/A | N/A |
| Library | Microsoft.Extensions.Diagnostics.HealthChecks | 8.x |
| Library | OpenTelemetry .NET | latest stable |
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

Implement the cross-cutting API middleware pipeline including: a health check endpoint at `GET /api/v1/health` returning JSON status with database connectivity awareness (NFR-005), global exception handling middleware that returns RFC 9457 Problem Details responses without exposing stack traces, an authentication/authorization middleware skeleton that returns structured HTTP 401 for unauthenticated requests (to be wired to real identity in EP-001), and structured logging with OpenTelemetry baseline instrumentation (NFR-011). This task delivers the API resilience and observability foundation required by all backend feature endpoints.

## Dependent Tasks

- task_001_be_aspnet_solution_scaffold (requires compiled solution with modular project structure and `Program.cs` composition root)

## Impacted Components

- Modified: `server/src/PropelIQ.Api/Program.cs` (middleware pipeline registration)
- Modified: `server/src/PropelIQ.Api/PropelIQ.Api.csproj` (NuGet packages for health checks, OpenTelemetry)
- New: `server/src/PropelIQ.Api/Middleware/GlobalExceptionMiddleware.cs` (exception handling)
- New: `server/src/PropelIQ.Api/Infrastructure/HealthChecks/DatabaseHealthCheck.cs` (custom DB health check)
- New: `server/src/PropelIQ.Api/Infrastructure/Auth/AuthenticationSetup.cs` (auth middleware configuration)
- New: `server/src/PropelIQ.SharedKernel/Errors/ProblemDetailsFactory.cs` (structured error response builder)
- Modified: `server/src/PropelIQ.Api/appsettings.json` (health check and logging configuration)
- Modified: `server/src/PropelIQ.Api/appsettings.Development.json` (development logging overrides)

## Implementation Plan

1. **Add health check infrastructure** by installing `Microsoft.Extensions.Diagnostics.HealthChecks` NuGet package. Register health checks in `Program.cs` with a self-check and a database connectivity check (returns `Degraded` when DB is unavailable). Map the health endpoint to `GET /api/v1/health` with a custom JSON response writer returning status, checks, and duration.

### ASP.NET Core 8 Health Check Configuration Reference

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck<DatabaseHealthCheck>("database",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "db", "ready" });

app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});
```

Source: ASP.NET Core 8.0.21 health checks documentation

2. **Implement global exception middleware** that catches unhandled exceptions, logs the full exception (including stack trace) to structured logs, and returns an RFC 9457 Problem Details JSON response with HTTP 500 status code. Stack traces are never exposed in the response body. Use ASP.NET Core 8's built-in `IProblemDetailsService` and `app.UseExceptionHandler()`.

### ASP.NET Core 8 Problem Details Exception Handler Reference

```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

app.UseExceptionHandler();
app.UseStatusCodePages();
```

Source: ASP.NET Core 8 RFC 9457 Problem Details built-in support

3. **Configure authentication middleware skeleton** using JWT Bearer scheme registration. The middleware validates that protected endpoints (those decorated with `[Authorize]`) reject unauthenticated requests with HTTP 401 and a structured Problem Details response. JWT validation settings use placeholder values to be replaced in EP-001 with real identity provider configuration.

### ASP.NET Core 8 Auth Challenge with Structured Response

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/problem+json";
                var problem = new ProblemDetails
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "Authentication is required to access this resource.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                };
                await context.Response.WriteAsJsonAsync(problem);
            }
        };
    });

builder.Services.AddAuthorization();
```

Source: ASP.NET Core 8 JWT Bearer authentication with custom challenge handler

4. **Set up structured logging** using the built-in logging framework with structured JSON output. Install OpenTelemetry packages (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`) and configure trace and metrics exporters for the baseline observability pipeline (NFR-011).

5. **Configure middleware pipeline order** in `Program.cs`:
   - `app.UseExceptionHandler()` (first - catches all exceptions)
   - `app.UseStatusCodePages()` (structured status code responses)
   - `app.UseAuthentication()` (JWT validation)
   - `app.UseAuthorization()` (policy enforcement)
   - `app.MapControllers()` (endpoint routing)
   - `app.MapHealthChecks()` (health endpoint - unauthenticated)

6. **Create a sample protected controller** with an `[Authorize]` attribute on one endpoint to verify the 401 structured response for unauthenticated requests.

7. **Configure appsettings.json** with health check intervals, JWT placeholder values, logging levels, and OpenTelemetry exporter endpoint.

8. **Validate all acceptance criteria** by running the API and testing health endpoint, exception handling, and auth challenge responses.

## Current Project State

```text
server/
├── PropelIQ.sln
├── src/
│   ├── PropelIQ.Api/
│   │   ├── PropelIQ.Api.csproj
│   │   ├── Program.cs
│   │   ├── Controllers/
│   │   │   └── BaseApiController.cs
│   │   └── appsettings.json
│   ├── PropelIQ.SharedKernel/
│   │   ├── PropelIQ.SharedKernel.csproj
│   │   └── BaseEntity.cs
│   └── Modules/
│       ├── Scheduling/
│       │   ├── PropelIQ.Modules.Scheduling.Api/
│       │   ├── PropelIQ.Modules.Scheduling.Application/
│       │   ├── PropelIQ.Modules.Scheduling.Domain/
│       │   └── PropelIQ.Modules.Scheduling.Infrastructure/
│       ├── ClinicalIntelligence/
│       │   └── (same 4-layer structure)
│       ├── Administration/
│       │   └── (same 4-layer structure)
│       └── SharedServices/
│           └── (same 4-layer structure)
```

> Assumes task_001 is completed. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/PropelIQ.Api/PropelIQ.Api.csproj | Add health checks, OpenTelemetry, and JWT Bearer NuGet packages |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register health checks, exception handler, auth middleware, OpenTelemetry, and pipeline order |
| CREATE | server/src/PropelIQ.Api/Middleware/GlobalExceptionMiddleware.cs | Custom exception handling with structured logging (fallback if built-in is insufficient) |
| CREATE | server/src/PropelIQ.Api/Infrastructure/HealthChecks/DatabaseHealthCheck.cs | Custom IHealthCheck returning Degraded when DB unavailable |
| CREATE | server/src/PropelIQ.Api/Infrastructure/HealthChecks/HealthCheckResponseWriter.cs | JSON response writer for health endpoint |
| CREATE | server/src/PropelIQ.Api/Infrastructure/Auth/AuthenticationSetup.cs | Extension method for JWT Bearer auth registration with custom challenge |
| CREATE | server/src/PropelIQ.SharedKernel/Errors/ProblemDetailsFactory.cs | Utility for building structured Problem Details responses |
| MODIFY | server/src/PropelIQ.Api/appsettings.json | Add Jwt, HealthChecks, Logging, and OpenTelemetry configuration sections |
| CREATE | server/src/PropelIQ.Api/appsettings.Development.json | Development-specific logging levels and OTLP exporter endpoint |
| CREATE | server/src/PropelIQ.Api/Controllers/DiagnosticsController.cs | Sample protected endpoint with [Authorize] for auth validation |

## External References

- ASP.NET Core 8 health checks: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0
- ASP.NET Core 8 error handling and Problem Details: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0
- RFC 9457 Problem Details for HTTP APIs: https://www.rfc-editor.org/rfc/rfc9457
- ASP.NET Core 8 JWT Bearer authentication: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-8.0
- OpenTelemetry .NET instrumentation: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel
- ASP.NET Core 8 middleware pipeline order: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-8.0
- ASP.NET Core 8 source (v8.0.21): https://github.com/dotnet/aspnetcore/blob/v8.0.21

## Build Commands

```bash
# Build solution
dotnet build server/PropelIQ.sln

# Run API
dotnet run --project server/src/PropelIQ.Api/PropelIQ.Api.csproj

# Test health endpoint
curl http://localhost:5000/api/v1/health

# Test 401 on protected endpoint
curl -i http://localhost:5000/api/v1/diagnostics/protected
```

## Implementation Validation Strategy

- [ ] `GET /api/v1/health` returns HTTP 200 with JSON payload containing `status`, `checks`, and `totalDuration`
- [ ] Health endpoint responds within 500 ms (NFR-002)
- [ ] Health check returns `Degraded` status when database connection is unavailable
- [ ] Unhandled controller exception returns HTTP 500 with RFC 9457 Problem Details JSON (no stack trace)
- [ ] Unauthenticated request to `[Authorize]` endpoint returns HTTP 401 with Problem Details JSON
- [ ] Structured logging outputs JSON format with correlation/trace IDs
- [ ] OpenTelemetry traces are emitted for HTTP requests
- [ ] `dotnet build server/PropelIQ.sln` compiles with zero errors after changes

## Implementation Checklist

- [ ] Install NuGet packages: `Microsoft.Extensions.Diagnostics.HealthChecks`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`
- [ ] Create `DatabaseHealthCheck` implementing `IHealthCheck` with `Degraded` status on connection failure
- [ ] Register health checks and map `GET /api/v1/health` with custom JSON response writer in `Program.cs`
- [ ] Configure `AddProblemDetails()` and `UseExceptionHandler()` for RFC 9457 structured error responses
- [ ] Configure JWT Bearer authentication skeleton with custom `OnChallenge` returning Problem Details 401
- [ ] Register OpenTelemetry tracing and metrics for ASP.NET Core instrumentation (NFR-011)
- [ ] Set correct middleware pipeline order in `Program.cs` (exception handler -> auth -> authorization -> endpoints)
- [ ] Validate all endpoints: health returns 200 JSON, protected returns 401 Problem Details, exception returns 500 Problem Details

using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PropelIQ.Api.Authorization;
using PropelIQ.Api.Authorization.Handlers;
using PropelIQ.Api.Authorization.Policies;
using PropelIQ.Api.Hubs;
using PropelIQ.Api.Infrastructure.Auth;
using PropelIQ.Api.Infrastructure.HealthChecks;
using PropelIQ.Api.Infrastructure.Tenancy;
using PropelIQ.Api.Infrastructure;
using PropelIQ.Api.Sessions;
using PropelIQ.Modules.Administration.Application.Auth.Validators;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.Observability;
using PropelIQ.SharedKernel.Persistence;
using PropelIQ.Modules.Scheduling.Infrastructure;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure;
using PropelIQ.Modules.Administration.Infrastructure;
using PropelIQ.Modules.Insurance.Infrastructure;
using PropelIQ.Modules.SharedServices.Infrastructure;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.Modules.SharedServices.Infrastructure.Identity;
using System.Threading.RateLimiting;

// ─────────────────────────────────────────────────────────────────────────────
// PropelIQ API — Composition Root
// TR-001: Modular layered architecture — all module registrations are wired here.
// TR-002: Versioned REST API prefix applied via BaseApiController route attribute.
// NFR-005: Health checks provide degraded-mode startup when DB is unavailable.
// NFR-011: OpenTelemetry traces and metrics baseline instrumentation.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── CORS ────────────────────────────────────────────────────────────────────
// Allow the Angular dev server origin during local development.
// In production this should be restricted to the actual frontend domain.
var angularOrigin = builder.Configuration.GetValue<string>("Cors:AllowedOrigin") ?? "http://localhost:4200";
builder.Services.AddCors(options =>
    options.AddPolicy("PropelIQCors", policy =>
        policy.WithOrigins(angularOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              // Required for SignalR WebSocket negotiation (us_017).
              .AllowCredentials()));

// ── SignalR (us_017: real-time session notifications) ────────────────────────
// AllowCredentials + explicit origin required for SignalR WebSocket handshake.
builder.Services.AddSignalR();

// ── MVC / API Controllers ────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── FluentValidation ─────────────────────────────────────────────────────────
// Auto-validation: invalid [FromBody] payloads return 400 before the action runs.
// Validators are discovered from each module's Application assembly and the API assembly.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<PropelIQ.Api.Validators.InviteStaffRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<PropelIQ.Modules.Scheduling.Application.Intake.Validators.SaveDraftRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<PropelIQ.Modules.SharedServices.Application.Compliance.Validators.ReportRequestValidator>();

// ── Rate Limiting (auth endpoints) ───────────────────────────────────────────
// OWASP A07 / AC-4: cap auth requests per IP to prevent brute-force abuse.
// Limits are read from configuration so Development appsettings can set high
// values (e.g. 1000) to avoid blocking during local testing.
builder.Services.AddRateLimiter(options =>
{
    var isDev = builder.Environment.IsDevelopment();
    options.AddFixedWindowLimiter("register-policy", opt =>
    {
        opt.PermitLimit = isDev ? 1000 : 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    // OWASP A07: cap login attempts per IP — 10 per 5 minutes balances usability vs brute-force.
    options.AddFixedWindowLimiter("login-policy", opt =>
    {
        opt.PermitLimit = isDev ? 1000 : 10;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("otp-policy", opt =>
    {
        opt.PermitLimit = isDev ? 1000 : 3;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    // NFR-012: max 10 staff invitations per 15 minutes per Admin (us_016).
    options.AddFixedWindowLimiter("invite-policy", opt =>
    {
        opt.PermitLimit = isDev ? 1000 : 10;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    // OWASP A07 / us_018 edge case: max 3 password-reset requests per 15 minutes.
    options.AddFixedWindowLimiter("password-reset-policy", opt =>
    {
        opt.PermitLimit = isDev ? 1000 : 3;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Problem Details (RFC 9457) ───────────────────────────────────────────────
// Adds IProblemDetailsService and configures built-in exception handler to return
// structured Problem Details JSON for all unhandled errors (US_002 Edge Case).
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        // Inject correlation trace ID into every problem details response.
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        // Never expose internal details in non-development environments.
        if (!ctx.HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            ctx.ProblemDetails.Detail = null;
        }
        else
        {
            // In Development, expose the full exception so failing requests can
            // be diagnosed without attaching a debugger.
            var ex = ctx.HttpContext.Features
                .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            if (ex is not null)
                ctx.ProblemDetails.Extensions["exception"] = ex.ToString();
        }
    };
});

// ── Authentication / Authorization ───────────────────────────────────────────
// JWT Bearer skeleton — AC-4: unauthenticated requests return 401 Problem Details.
// Replaced with real IdP config in EP-001.
builder.Services.AddPropelIQAuthentication(builder.Configuration);

// ── DataProtection token lifespan (us_016) ────────────────────────────────────
// Sets the TTL for DataProtection-based tokens (TokenOptions.DefaultProvider).
// Used for staff invitation tokens — 48h matches the invitation expiry window (AC-1).
// The OTP email-confirmation flow uses the TOTP EmailTokenProvider and is unaffected.
builder.Services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(
    opts => opts.TokenLifespan = TimeSpan.FromHours(48));

// ── Password-reset token lifespan (us_018 edge case) ─────────────────────────
// Named "PasswordReset" provider uses a separate 24-hour TTL so staff invite tokens
// remain at 48h. AddTokenProvider registers this provider in SharedServicesServiceRegistration.
builder.Services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(
    "PasswordReset",
    opts => opts.TokenLifespan = TimeSpan.FromHours(24));

// ── RBAC Authorization Policies (EP-001 us_015) ──────────────────────────────
// Named policies: PatientOnly, StaffOnly, AdminOnly, StaffOrAdmin, PatientResourceOwner.
// FallbackPolicy requires authentication on all endpoints not marked [AllowAnonymous].
builder.Services.AddHttpContextAccessor();
builder.Services.AddAppAuthorizationPolicies();
builder.Services.AddScoped<IAuthorizationHandler, PatientResourceAuthorizationHandler>();
// AuditAuthorizationHandler registered AFTER PatientResourceAuthorizationHandler
// so it sees the final state of context.HasFailed / PendingRequirements.
builder.Services.AddScoped<IAuthorizationHandler, AuditAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenResultHandler>();

// ── Redis Distributed Cache ──────────────────────────────────────────────────
// TR-004: distributed cache for hot slot search and profile read acceleration.
// InstanceName prefixes every key with "PropelIQ:" to avoid collisions.
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "PropelIQ:";
});

// ── Health Checks ─────────────────────────────────────────────────────────────
// NFR-005: health endpoint returns Degraded when DB or Redis is unavailable.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API host is running."))
    .AddCheck<DatabaseHealthCheck>(
        "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: ["db", "ready"])
    .AddCheck<RedisHealthCheck>(
        "redis",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: ["cache", "ready"]);

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
// NFR-011: full OTel SDK — traces (OTLP + console), metrics (Prometheus + OTLP),
// and structured logs (OTLP + console) with correlation ID propagation.
// Edge case: console exporter active always; OTLP used only when endpoint is set.
builder.Services.AddPropelIQTelemetry(builder.Configuration);

// ── API Documentation (Swagger / OpenAPI) ────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "PropelIQ API", Version = "v1" });

    // Enable JWT Bearer auth in Swagger UI — paste the token from /api/v1/auth/login.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT access token from POST /api/v1/auth/login.\nExample: eyJhbG..."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// ── AI Gateway Client ──────────────────────────────────────────────────────
// AIR-005/AC-2: typed HttpClient with Polly retry + circuit breaker pointing at
// the LiteLLM proxy. Config validated at startup; malformed section = fast fail.
builder.Services.AddAiGateway(builder.Configuration);

// Development override: replace LiteLlmGatewayClient with a local keyword-based
// mock so AI-assist works without a running LiteLLM proxy or Azure OpenAI credentials.
// The last IAiGatewayClient registration wins in ASP.NET Core DI.
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IAiGatewayClient, DevMockAiGatewayClient>();
// ── Module Infrastructure Registrations ──────────────────────────────────────
// TimeProvider singleton used by reminder scheduling for testable UTC clock access.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddSchedulingInfrastructure(builder.Configuration)
    .AddClinicalIntelligenceInfrastructure(builder.Configuration)
    .AddAdministrationInfrastructure(builder.Configuration)
    .AddInsuranceInfrastructure(builder.Configuration)
    .AddSharedServicesInfrastructure(builder.Configuration);

// ── Unit of Work & Bulk Import (DR-002, AC-2) ───────────────────────────────
// UnitOfWork wraps AppDbContext with explicit transaction management.
// BulkImportProcessor batches entities in a single transaction with per-row error reporting.
builder.Services.AddScoped<IUnitOfWork>(sp =>
    new UnitOfWork(sp.GetRequiredService<AppDbContext>()));
builder.Services.AddScoped<BulkImportProcessor>(sp =>
    new BulkImportProcessor(
        sp.GetRequiredService<IUnitOfWork>(),
        sp.GetRequiredService<AppDbContext>()));

// ── Session Management (us_017) ──────────────────────────────────────────────
// ISessionService: session create/extend/invalidate with single-session enforcement.
// SessionCleanupService: background worker purging idle sessions every 5 minutes.
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddHostedService<SessionCleanupService>();

// ── Account Lockout Handler (us_018) ─────────────────────────────────────────
// Handles lockout events: invalidates sessions, revokes tokens, sends email (AC-3).
builder.Services.AddScoped<PropelIQ.Api.AccountLockoutHandler>();

// ── Patient Data Access Filter (us_057, AC-1) ─────────────────────────────────
// Registered as scoped so it can be injected via [ServiceFilter(typeof(PatientDataAccessFilter))]
// on patient-data controllers. Emits DataAccess audit events on successful reads.
builder.Services.AddScoped<PropelIQ.Api.Filters.PatientDataAccessFilter>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// ── Database Migrations (NFR-005: auto-migration on startup for dev/test)
// Apply all pending EF Core migrations during startup. This ensures the schema
// is ready before the first request arrives. In production, run migrations
// explicitly via DbMigrator or CI/CD pipeline instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await authDb.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply database migrations. API will fail on first request.");
        if (!app.Environment.IsDevelopment())
        {
            throw; // Fail fast in production
        }
    }
}

// ── Dev Identity Role Seed ────────────────────────────────────────────────────
// Ensures all Identity roles exist and backfills any user whose auth.AspNetUserRoles
// row is missing (e.g. accounts created before the registration role-sync patch).
// The role source-of-truth is app.users.role; this block keeps auth in sync.
// Runs in all environments: safe because it is fully idempotent (ON CONFLICT / skip).
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var appDb       = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 1. Ensure all four Identity roles exist.
    string[] allRoles = ["Admin", "Clinician", "Staff", "Patient"];
    foreach (var role in allRoles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role, NormalizedName = role.ToUpperInvariant() });
    }

    // 2. For every auth user whose corresponding app.users row has a role set,
    //    assign that role in Identity if not already assigned.
    var appUsers = await appDb.Users
        .Where(u => !string.IsNullOrEmpty(u.Role))
        .Select(u => new { u.Id, u.Email, u.Role })
        .ToListAsync();

    foreach (var appUser in appUsers)
    {
        var identityUser = await userManager.FindByIdAsync(appUser.Id.ToString());
        if (identityUser is null) continue;

        var existingRoles = await userManager.GetRolesAsync(identityUser);

        // Remove stale roles that no longer match app.users.role
        var rolesToRemove = existingRoles.Where(r => r != appUser.Role).ToList();
        if (rolesToRemove.Count > 0)
            await userManager.RemoveFromRolesAsync(identityUser, rolesToRemove);

        // Add the correct role if not already present
        if (!existingRoles.Contains(appUser.Role))
            await userManager.AddToRoleAsync(identityUser, appUser.Role);
    }

    app.Logger.LogInformation("Identity role seed completed: {Count} app users processed", appUsers.Count);
}

// ── Middleware Pipeline Order ─────────────────────────────────────────────────
// 1. Exception handler — MUST be first to catch all downstream exceptions.
app.UseExceptionHandler();
// 2. Structured status code responses (e.g. 404, 405 from routing).
app.UseStatusCodePages();
// 2a. Correlation ID — runs early so every downstream log carries the ID (AC-4).
app.UseMiddleware<CorrelationIdMiddleware>();

// 2b. AI fallback envelope — injects "aiFallbackActive": true into JSON responses
//     while the AI gateway circuit is open (US_053, Edge Case 2, AC-2).
//     Must run BEFORE auth so unauthenticated 401 responses also carry the flag.
//     Fast path: no-op (no buffering) when circuit is closed.
app.UseMiddleware<AiFallbackEnvelopeMiddleware>();

// ── Developer Tools ───────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PropelIQ API v1");
    });
}

// 3. CORS — must come before auth so preflight OPTIONS requests are handled.
// Uses the named policy registered in AddCors above (includes AllowCredentials for SignalR).
app.UseCors("PropelIQCors");

// 3a. HTTPS redirect before auth.
app.UseHttpsRedirection();

// 3a. Rate limiter — before auth so limits apply to unauthenticated callers too.
app.UseRateLimiter();

// 4. Authentication — validates JWT bearer token.
app.UseAuthentication();

// 5. Authorization — enforces [Authorize] policies.
app.UseAuthorization();

// 5a. Tenant context — sets app.current_tenant_id session variable for RLS.
app.UseMiddleware<TenantContextMiddleware>();

// 6. Controller endpoints — RequireAuthorization() makes all controller actions
//    require authentication by default; [AllowAnonymous] on individual actions
//    exempts public endpoints (AC-4). This scopes enforcement to controllers only
//    so health/metrics endpoints remain anonymous without extra metadata.
app.MapControllers().RequireAuthorization();

// 6b. SignalR hub — requires JWT auth (configured via Authorize on SessionHub).
// Excluded from the MapControllers() RequireAuthorization scope so it uses its own [Authorize].
app.MapHub<SessionHub>("/hubs/session");

// 7. Health endpoint — unauthenticated, responds at /api/v1/health (AC-2).
app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

// 8. Prometheus metrics scraping endpoint (GET /metrics).
// Excluded from trace collection by the OTel ASP.NET Core instrumentation filter.
app.UseOpenTelemetryPrometheusScrapingEndpoint();



app.Run();

// Partial class exposed for integration test WebApplicationFactory<Program>.
public partial class Program { }

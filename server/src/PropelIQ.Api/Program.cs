using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PropelIQ.Api.Infrastructure.Auth;
using PropelIQ.Api.Infrastructure.HealthChecks;
using PropelIQ.Api.Infrastructure.Tenancy;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.Observability;
using PropelIQ.SharedKernel.Persistence;
using PropelIQ.Modules.Scheduling.Infrastructure;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure;
using PropelIQ.Modules.Administration.Infrastructure;
using PropelIQ.Modules.SharedServices.Infrastructure;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

// ─────────────────────────────────────────────────────────────────────────────
// PropelIQ API — Composition Root
// TR-001: Modular layered architecture — all module registrations are wired here.
// TR-002: Versioned REST API prefix applied via BaseApiController route attribute.
// NFR-005: Health checks provide degraded-mode startup when DB is unavailable.
// NFR-011: OpenTelemetry traces and metrics baseline instrumentation.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── MVC / API Controllers ────────────────────────────────────────────────────
builder.Services.AddControllers();

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
    };
});

// ── Authentication / Authorization ───────────────────────────────────────────
// JWT Bearer skeleton — AC-4: unauthenticated requests return 401 Problem Details.
// Replaced with real IdP config in EP-001.
builder.Services.AddPropelIQAuthentication(builder.Configuration);

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
});
// ── AI Gateway Client ──────────────────────────────────────────────────────
// AIR-005/AC-2: typed HttpClient with Polly retry + circuit breaker pointing at
// the LiteLLM proxy. Config validated at startup; malformed section = fast fail.
builder.Services.AddAiGateway(builder.Configuration);
// ── Module Infrastructure Registrations ──────────────────────────────────────
builder.Services
    .AddSchedulingInfrastructure(builder.Configuration)
    .AddClinicalIntelligenceInfrastructure(builder.Configuration)
    .AddAdministrationInfrastructure(builder.Configuration)
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

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// ── Middleware Pipeline Order ─────────────────────────────────────────────────
// 1. Exception handler — MUST be first to catch all downstream exceptions.
app.UseExceptionHandler();
// 2. Structured status code responses (e.g. 404, 405 from routing).
app.UseStatusCodePages();
// 2a. Correlation ID — runs early so every downstream log carries the ID (AC-4).
app.UseMiddleware<CorrelationIdMiddleware>();

// ── Developer Tools ───────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PropelIQ API v1");
    });
}

// 3. HTTPS redirect before auth.
app.UseHttpsRedirection();

// 4. Authentication — validates JWT bearer token.
app.UseAuthentication();

// 5. Authorization — enforces [Authorize] policies.
app.UseAuthorization();

// 5a. Tenant context — sets app.current_tenant_id session variable for RLS.
app.UseMiddleware<TenantContextMiddleware>();

// ── Routing ───────────────────────────────────────────────────────────────────
// 6. Controller endpoints.
app.MapControllers();

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

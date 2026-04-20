# Task - TASK_001

## Requirement Reference

- User Story: us_007
- Story Location: .propel/context/tasks/EP-TECH/us_007/us_007.md
- Acceptance Criteria:
  - AC-1: Given the API is running with OpenTelemetry configured, When an HTTP request is processed, Then a distributed trace is emitted with span data including service name, route, duration, and status code.
  - AC-2: Given an external call to the AI gateway, email, or SMS provider is made, Then a child span is recorded under the parent request trace with the provider name and response status.
  - AC-4: Given an error occurs at any layer, When it is logged, Then the log entry includes correlation ID, severity, module name, and structured key-value pairs compatible with Loki query syntax.
- Edge Case:
  - What happens if the telemetry exporter is unreachable? Exporter falls back to console output; application continues without blocking request processing.
  - How does the system handle high cardinality trace tags? Cardinality-generating values like patient IDs are hashed before use as trace attributes.

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
| Library | OpenTelemetry.Extensions.Hosting | 1.x (latest stable) |
| Library | OpenTelemetry.Instrumentation.AspNetCore | 1.x (latest stable) |
| Library | OpenTelemetry.Instrumentation.Http | 1.x (latest stable) |
| Library | OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.x (latest stable) |
| Library | OpenTelemetry.Exporter.Console | 1.x (latest stable) |
| Library | OpenTelemetry.Exporter.Prometheus.AspNetCore | 1.x (latest stable) |
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

Integrate OpenTelemetry instrumentation into the ASP.NET Core Web API to emit distributed traces, custom metrics, and structured logs for all critical operations. The implementation configures the OpenTelemetry SDK via `AddOpenTelemetry()` with ASP.NET Core and HttpClient auto-instrumentation, a Prometheus scraping endpoint for metrics, OTLP export for traces and logs, and a console fallback exporter for resilience when the collector is unreachable. Custom `ActivitySource` spans wrap external provider calls (AI gateway, email, SMS) to produce child spans with provider name and response status. Structured logging uses Serilog with OpenTelemetry enrichment to include correlation ID (`TraceId`), severity, module name, and key-value pairs compatible with Loki query syntax. High-cardinality values (e.g., patient IDs) are SHA-256 hashed before use as trace attributes.

## Dependent Tasks

- US_002 tasks (requires ASP.NET Core solution with compilable projects and middleware pipeline)

## Impacted Components

- Modify: `server/src/PropelIQ.Api/Program.cs` (register OpenTelemetry services and middleware)
- New: `server/src/SharedKernel/Observability/DiagnosticsConfig.cs` (ActivitySource, Meter, and service name constants)
- New: `server/src/SharedKernel/Observability/TelemetryServiceCollectionExtensions.cs` (IServiceCollection extension for OTel setup)
- New: `server/src/SharedKernel/Observability/CorrelationIdMiddleware.cs` (middleware to propagate/create correlation IDs)
- New: `server/src/SharedKernel/Observability/CardinalityHasher.cs` (utility to hash high-cardinality values)
- Modify: `server/src/PropelIQ.Api/PropelIQ.Api.csproj` (add OpenTelemetry NuGet packages)
- Modify: `server/src/SharedKernel/SharedKernel.csproj` (add OpenTelemetry NuGet packages)

## Implementation Plan

1. **Add OpenTelemetry NuGet packages** to the API and SharedKernel projects:

```xml
<!-- PropelIQ.Api.csproj -->
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.*" />
```

2. **Create `DiagnosticsConfig.cs`** in SharedKernel to define centralized telemetry constants. This is the single source of truth for the ActivitySource and Meter names used across all modules:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PropelIQ.SharedKernel.Observability;

public static class DiagnosticsConfig
{
    public const string ServiceName = "PropelIQ.Api";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Custom metrics
    public static readonly Counter<long> RequestCounter =
        Meter.CreateCounter<long>("propeliq.http.requests", "{requests}", "Total HTTP requests");
    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("propeliq.http.duration", "ms", "HTTP request duration");
    public static readonly Counter<long> ExternalCallCounter =
        Meter.CreateCounter<long>("propeliq.external.calls", "{calls}", "External provider calls");
    public static readonly Counter<long> ErrorCounter =
        Meter.CreateCounter<long>("propeliq.errors", "{errors}", "Application errors");
}
```

3. **Create `TelemetryServiceCollectionExtensions.cs`** to encapsulate the full OpenTelemetry SDK configuration as an `IServiceCollection` extension method. Configure all three signals (traces, metrics, logs) with OTLP export as primary and console as fallback:

```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static IServiceCollection AddPropelIQTelemetry(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"]
        ?? "http://localhost:4317";

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: DiagnosticsConfig.ServiceName,
                serviceVersion: DiagnosticsConfig.ServiceVersion,
                serviceInstanceId: Environment.MachineName))
        .WithTracing(tracing => tracing
            .AddSource(DiagnosticsConfig.ServiceName)
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
                opts.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation(opts =>
            {
                opts.RecordException = true;
            })
            .AddOtlpExporter(opts =>
                opts.Endpoint = new Uri(otlpEndpoint))
            .AddConsoleExporter()) // Fallback: AC edge case
        .WithMetrics(metrics => metrics
            .AddMeter(DiagnosticsConfig.ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter()
            .AddOtlpExporter(opts =>
                opts.Endpoint = new Uri(otlpEndpoint)));

    return services;
}
```

4. **Configure structured logging** in `Program.cs` using the OpenTelemetry logging provider. Clear default providers and add OTLP + console exporters. Each log entry automatically includes `TraceId` (correlation ID), severity, and the logger category name (module name):

```csharp
builder.Logging.ClearProviders();
builder.Services.AddOpenTelemetry()
    .WithLogging(logging => logging
        .AddOtlpExporter(opts =>
            opts.Endpoint = new Uri(otlpEndpoint))
        .AddConsoleExporter());
```

Structured log format compatible with Loki query syntax:
```
{severity="Error", module="PropelIQ.Scheduling", correlation_id="abc123"} |= "SlotNotFound"
```

5. **Create `CorrelationIdMiddleware.cs`** that reads or generates a correlation ID per request and stores it in `Activity.Current?.SetTag("correlation_id", id)` and in the logging scope. This ensures AC-4 compliance — every log entry carries the correlation ID:

```csharp
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationIdHeader] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        using (context.RequestServices.GetRequiredService<ILogger<CorrelationIdMiddleware>>()
            .BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["Module"] = "PropelIQ.Api"
            }))
        {
            await next(context);
        }
    }
}
```

6. **Create `CardinalityHasher.cs`** utility to hash high-cardinality values before using them as trace attributes (AC edge case). Patient IDs and similar identifiers are SHA-256 hashed to prevent cardinality explosion in the telemetry backend:

```csharp
using System.Security.Cryptography;
using System.Text;

public static class CardinalityHasher
{
    public static string HashForTrace(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes)[..16]; // Truncated 16-char hex
    }
}
```

Usage in spans:
```csharp
using var activity = DiagnosticsConfig.ActivitySource.StartActivity("GetPatient");
activity?.SetTag("patient_id_hash", CardinalityHasher.HashForTrace(patientId));
```

7. **Implement external call child spans** for AC-2. Create a pattern for wrapping external provider calls (AI gateway, email, SMS) in child spans with provider name and response status:

```csharp
public async Task<TResult> ExecuteWithSpan<TResult>(
    string providerName,
    string operationName,
    Func<Task<TResult>> action)
{
    using var activity = DiagnosticsConfig.ActivitySource.StartActivity(
        $"{providerName}.{operationName}",
        ActivityKind.Client);

    activity?.SetTag("provider.name", providerName);
    activity?.SetTag("provider.operation", operationName);

    try
    {
        var result = action();
        activity?.SetTag("provider.status", "success");
        DiagnosticsConfig.ExternalCallCounter.Add(1,
            new("provider", providerName),
            new("status", "success"));
        return await result;
    }
    catch (Exception ex)
    {
        activity?.SetTag("provider.status", "error");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        DiagnosticsConfig.ExternalCallCounter.Add(1,
            new("provider", providerName),
            new("status", "error"));
        throw;
    }
}
```

8. **Register middleware and expose Prometheus endpoint** in `Program.cs`:

```csharp
// In service registration
builder.Services.AddPropelIQTelemetry(builder.Configuration);

// In middleware pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseOpenTelemetryPrometheusScrapingEndpoint(); // GET /metrics
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       ├── PropelIQ.Api/
│       │   ├── PropelIQ.Api.csproj
│       │   ├── Program.cs
│       │   └── Controllers/
│       ├── SharedKernel/
│       │   └── SharedKernel.csproj
│       ├── Scheduling.Api/
│       ├── Scheduling.Application/
│       ├── Scheduling.Domain/
│       └── Scheduling.Infrastructure/
├── docker-compose.yml
└── .env.example
```

> Placeholder: Update on execution based on US_002 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/PropelIQ.Api/PropelIQ.Api.csproj | Add OpenTelemetry NuGet package references |
| MODIFY | server/src/SharedKernel/SharedKernel.csproj | Add OpenTelemetry.Api and Abstractions packages |
| CREATE | server/src/SharedKernel/Observability/DiagnosticsConfig.cs | Centralized ActivitySource, Meter, and custom metric definitions |
| CREATE | server/src/SharedKernel/Observability/TelemetryServiceCollectionExtensions.cs | IServiceCollection extension for full OTel SDK configuration |
| CREATE | server/src/SharedKernel/Observability/CorrelationIdMiddleware.cs | Middleware to propagate/create correlation IDs on every request |
| CREATE | server/src/SharedKernel/Observability/CardinalityHasher.cs | SHA-256 utility for hashing high-cardinality trace attributes |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register OTel services, correlation middleware, and Prometheus endpoint |

## External References

- OpenTelemetry .NET SDK getting started: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/getting-started-aspnetcore/README.md
- OpenTelemetry ASP.NET Core instrumentation: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry Prometheus exporter for ASP.NET Core: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md
- OpenTelemetry OTLP exporter: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md
- OpenTelemetry logging integration: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/logs/getting-started-aspnetcore/README.md
- OpenTelemetry custom metrics (.NET Meter API): https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/getting-started-aspnetcore/README.md
- System.Diagnostics.Activity API: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity
- Loki log query syntax: https://grafana.com/docs/loki/latest/logql/

## Build Commands

```bash
# Restore and build with new packages
dotnet restore server/PropelIQ.sln
dotnet build server/PropelIQ.sln --configuration Release

# Run API locally with telemetry (console exporter active)
dotnet run --project server/src/PropelIQ.Api

# Verify Prometheus metrics endpoint
curl http://localhost:5000/metrics
```

## Implementation Validation Strategy

- [ ] OpenTelemetry SDK registers without errors on application startup
- [ ] HTTP request to any API endpoint emits a distributed trace with service name, route, duration, and status code (AC-1)
- [ ] External provider call (AI gateway, email, SMS) records a child span under the parent request trace with provider name and response status (AC-2)
- [ ] Error log entries include correlation ID, severity, module name, and structured key-value pairs (AC-4)
- [ ] Prometheus scraping endpoint (`GET /metrics`) returns request rate, error rate, and latency metrics
- [ ] Console exporter outputs telemetry when OTLP endpoint is unreachable (edge case)
- [ ] High-cardinality values (patient IDs) are hashed before use as trace attributes (edge case)
- [ ] Health check endpoints (`/health`) are excluded from trace collection

## Implementation Checklist

- [x] Add OpenTelemetry NuGet packages to `PropelIQ.Api.csproj` and `SharedKernel.csproj`
- [x] Create `DiagnosticsConfig.cs` with centralized `ActivitySource`, `Meter`, and custom metric instruments
- [x] Create `TelemetryServiceCollectionExtensions.cs` with `AddPropelIQTelemetry()` configuring traces (OTLP + console fallback), metrics (Prometheus + OTLP), and logging (OTLP + console)
- [x] Create `CorrelationIdMiddleware.cs` to propagate/generate correlation IDs and enrich `Activity.Current` and logging scope
- [x] Create `CardinalityHasher.cs` with SHA-256 hashing for high-cardinality trace attributes
- [x] Register OpenTelemetry services and middleware in `Program.cs` including Prometheus scraping endpoint at `/metrics`
- [x] Implement external call child span pattern (`ExecuteWithSpanAsync`) for AI gateway, email, and SMS provider calls
- [x] Configure `appsettings.json` with `OpenTelemetry:OtlpEndpoint` setting defaulting to `http://localhost:4317`

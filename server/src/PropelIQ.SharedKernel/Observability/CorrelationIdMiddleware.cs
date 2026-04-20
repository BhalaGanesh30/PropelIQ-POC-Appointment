using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PropelIQ.SharedKernel.Observability;

/// <summary>
/// ASP.NET Core middleware that propagates or generates a correlation ID for every
/// incoming HTTP request.
///
/// Priority order for correlation ID resolution:
///   1. Incoming X-Correlation-Id request header (caller-supplied).
///   2. Active trace ID from Activity.Current (set by OTel W3C TraceContext propagation).
///   3. New random GUID.
///
/// The resolved ID is:
///   - Echoed in the X-Correlation-Id response header.
///   - Set as the "correlation_id" tag on Activity.Current for trace linking.
///   - Injected into the ILogger scope so every log entry within the request
///     carries CorrelationId and Module keys (AC-4: Loki-compatible structured logs).
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId =
            context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString("N");

        // Echo back so callers can correlate their own logs with API traces.
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Tag the current OpenTelemetry span so every child span inherits the ID.
        Activity.Current?.SetTag("correlation_id", correlationId);

        // Inject into ILogger scope — propagates to all loggers used within this request.
        // Keys follow Loki label convention: lowercase snake_case.
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["correlation_id"] = correlationId,
            ["module"] = "PropelIQ.Api"
        }))
        {
            await _next(context);
        }
    }
}

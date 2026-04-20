using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PropelIQ.SharedKernel.Observability;

/// <summary>
/// IServiceCollection extension that configures the full OpenTelemetry SDK:
/// traces, metrics, and logs — each with OTLP as the primary exporter and
/// console as the fallback when the collector is unreachable (Edge Case).
///
/// NFR-011: OTel baseline instrumentation for PropelIQ API.
/// AC-1: HTTP spans include service name, route, duration, status code.
/// AC-2: External provider calls produce child spans via AddHttpClientInstrumentation.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddPropelIQTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
        var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(otlpEndpoint);

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: DiagnosticsConfig.ServiceName,
                serviceVersion: DiagnosticsConfig.ServiceVersion,
                serviceInstanceId: Environment.MachineName);

        services.AddOpenTelemetry()

            // ── Traces ───────────────────────────────────────────────────────
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(DiagnosticsConfig.ServiceName)
                    // AC-1: ASP.NET Core auto-instrumentation (route, status, duration).
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        // Exclude health-check polling from trace noise.
                        opts.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health") &&
                            !httpContext.Request.Path.StartsWithSegments("/metrics");
                    })
                    // AC-2: HttpClient auto-instrumentation records child spans for
                    // outbound calls to AI gateway, email, SMS providers.
                    .AddHttpClientInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                    })
                    // Console exporter: active always as fallback (edge case: OTLP unreachable).
                    .AddConsoleExporter();

                if (hasOtlpEndpoint)
                {
                    tracing.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(otlpEndpoint!));
                }
            })

            // ── Metrics ──────────────────────────────────────────────────────
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(DiagnosticsConfig.ServiceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Prometheus scraping endpoint — registered in middleware via
                    // app.UseOpenTelemetryPrometheusScrapingEndpoint().
                    .AddPrometheusExporter();

                if (hasOtlpEndpoint)
                {
                    metrics.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(otlpEndpoint!));
                }
            });

        // ── Logging ──────────────────────────────────────────────────────────
        // OTel logging provider: includes TraceId automatically in each log record
        // (AC-4: correlation_id in every structured log entry).
        // LoggerProviderBuilder only accepts exporters; options are configured separately.
        services.Configure<OpenTelemetry.Logs.OpenTelemetryLoggerOptions>(opts =>
        {
            opts.IncludeScopes = true;
            opts.IncludeFormattedMessage = true;
        });

        services.AddOpenTelemetry()
            .WithLogging(otelLogging =>
            {
                otelLogging.SetResourceBuilder(resourceBuilder);
                // Console fallback — active always.
                otelLogging.AddConsoleExporter();

                if (hasOtlpEndpoint)
                {
                    otelLogging.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(otlpEndpoint!));
                }
            });

        return services;
    }

    /// <summary>
    /// Wraps an external provider call in a child OpenTelemetry span (AC-2).
    /// Records provider name, operation name, and outcome as span attributes.
    /// Increments <see cref="DiagnosticsConfig.ExternalCallCounter"/> with provider + status labels.
    /// </summary>
    public static async Task<TResult> ExecuteWithSpanAsync<TResult>(
        string providerName,
        string operationName,
        Func<Task<TResult>> action)
    {
        using var activity = DiagnosticsConfig.ActivitySource.StartActivity(
            $"{providerName}.{operationName}",
            System.Diagnostics.ActivityKind.Client);

        activity?.SetTag("provider.name", providerName);
        activity?.SetTag("provider.operation", operationName);

        try
        {
            var result = await action();
            activity?.SetTag("provider.status", "success");
            DiagnosticsConfig.ExternalCallCounter.Add(1,
                new KeyValuePair<string, object?>("provider", providerName),
                new KeyValuePair<string, object?>("status", "success"));
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("provider.status", "error");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            DiagnosticsConfig.ExternalCallCounter.Add(1,
                new KeyValuePair<string, object?>("provider", providerName),
                new KeyValuePair<string, object?>("status", "error"));
            throw;
        }
    }
}

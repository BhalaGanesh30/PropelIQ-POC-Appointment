using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PropelIQ.SharedKernel.Observability;

/// <summary>
/// Centralized OpenTelemetry configuration constants.
/// Single source of truth for ActivitySource, Meter, and metric instrument names
/// shared across all PropelIQ modules.
/// NFR-011: Traces and metrics baseline instrumentation.
/// </summary>
public static class DiagnosticsConfig
{
    public const string ServiceName = "PropelIQ.Api";
    public const string ServiceVersion = "1.0.0";

    // ── Tracing ──────────────────────────────────────────────────────────────
    // ActivitySource is the .NET equivalent of an OpenTelemetry Tracer.
    // AC-1: spans emitted per request with service name, route, duration, status.
    public static readonly ActivitySource ActivitySource =
        new(ServiceName, ServiceVersion);

    // ── Metrics ──────────────────────────────────────────────────────────────
    // Meter instruments are recorded by AddMeter(ServiceName) in the OTel SDK.
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    /// <summary>Total inbound HTTP requests (labelled by method and route).</summary>
    public static readonly Counter<long> RequestCounter =
        Meter.CreateCounter<long>(
            "propeliq.http.requests",
            unit: "{requests}",
            description: "Total HTTP requests processed by the API.");

    /// <summary>HTTP request latency histogram in milliseconds.</summary>
    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>(
            "propeliq.http.duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds.");

    /// <summary>Total outbound calls to external providers (AI gateway, email, SMS).</summary>
    public static readonly Counter<long> ExternalCallCounter =
        Meter.CreateCounter<long>(
            "propeliq.external.calls",
            unit: "{calls}",
            description: "Outbound calls to external providers labelled by provider and status.");

    /// <summary>Total application errors (unhandled exceptions).</summary>
    public static readonly Counter<long> ErrorCounter =
        Meter.CreateCounter<long>(
            "propeliq.errors",
            unit: "{errors}",
            description: "Application errors labelled by module and exception type.");
}

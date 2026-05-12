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

    // ── Coding Decision Metrics (US_051, AIR-007) ─────────────────────────────

    /// <summary>
    /// Incremented each time a clinician accepts an AI-generated coding suggestion.
    /// Tagged with <c>decision.id</c> and <c>patient.id</c>.
    /// Feeds the AIR-007 agreement rate dashboard (Edge Case 2).
    /// </summary>
    public static readonly Counter<long> AcceptDecisionCounter =
        Meter.CreateCounter<long>(
            "coding_decision.accept_count",
            unit: "{decisions}",
            description: "Total coding decisions accepted by clinicians (AIR-007).");

    /// <summary>
    /// Incremented each time a clinician modifies an AI-generated coding suggestion.
    /// Tagged with <c>decision.id</c> and <c>patient.id</c>.
    /// Feeds the AIR-007 agreement rate dashboard (Edge Case 2).
    /// </summary>
    public static readonly Counter<long> ModifyDecisionCounter =
        Meter.CreateCounter<long>(
            "coding_decision.modify_count",
            unit: "{decisions}",
            description: "Total coding decisions modified by clinicians (AIR-007).");

    /// <summary>
    /// Incremented each time a clinician rejects an AI-generated coding suggestion.
    /// Tagged with <c>decision.id</c> and <c>patient.id</c>.
    /// Feeds the AIR-007 agreement rate dashboard (Edge Case 2).
    /// </summary>
    public static readonly Counter<long> RejectDecisionCounter =
        Meter.CreateCounter<long>(
            "coding_decision.reject_count",
            unit: "{decisions}",
            description: "Total coding decisions rejected by clinicians (AIR-007).");

    // ── Code Search Metrics (US_052, FR-MC-004) ───────────────────────────────

    /// <summary>
    /// Histogram of code search query duration in milliseconds per request.
    /// Tagged with <c>query.type</c> and <c>results.count</c>.
    /// Feeds the NFR-002 ≤ 500ms p95 SLO dashboard.
    /// </summary>
    public static readonly Histogram<long> CodeSearchDurationHistogram =
        Meter.CreateHistogram<long>(
            "code_search.query_duration_ms",
            unit: "ms",
            description: "Code search query duration in milliseconds (NFR-002 SLO target: ≤ 500ms p95).");

    /// <summary>Incremented on each Redis cache hit for code search results.</summary>
    public static readonly Counter<long> CodeSearchHitCounter =
        Meter.CreateCounter<long>(
            "code_search.cache_hit_count",
            unit: "{hits}",
            description: "Number of code search requests served from Redis cache.");

    /// <summary>Incremented on each Redis cache miss for code search results.</summary>
    public static readonly Counter<long> CodeSearchMissCounter =
        Meter.CreateCounter<long>(
            "code_search.cache_miss_count",
            unit: "{misses}",
            description: "Number of code search requests that required a database query.");

    /// <summary>
    /// Incremented each time a clinician adds a code to their favorites (AC-3).
    /// Tagged with <c>code.type</c>.
    /// </summary>
    public static readonly Counter<long> CodeFavoriteAddCounter =
        Meter.CreateCounter<long>(
            "code_favorite.add_count",
            unit: "{favorites}",
            description: "Total code favorites added by clinicians (US_052, AC-3).");

    /// <summary>
    /// Incremented each time a clinician removes a code from their favorites (AC-4).
    /// Tagged with <c>code.type</c>.
    /// </summary>
    public static readonly Counter<long> CodeFavoriteRemoveCounter =
        Meter.CreateCounter<long>(
            "code_favorite.remove_count",
            unit: "{favorites}",
            description: "Total code favorites removed by clinicians (US_052, AC-4).");

    // ── AI Gateway Circuit Breaker Metrics (US_053, AIR-011) ─────────────────

    /// <summary>
    /// Incremented each time the AI gateway circuit breaker trips to <c>open</c> (AC-2).
    /// Tagged with <c>circuit.state</c> ("open").
    /// Feeds the operations alert dashboard — high frequency indicates provider instability.
    /// </summary>
    public static readonly Counter<long> AiCircuitTripCounter =
        Meter.CreateCounter<long>(
            "ai.circuit_trip_count",
            unit: "{trips}",
            description: "Total AI gateway circuit breaker trips (US_053, AC-2).");

    /// <summary>
    /// Incremented when the circuit trips ≥ 3 times within a single clock-hour (Edge Case 1).
    /// Tagged with <c>hour</c> (yyyyMMddHH format, UTC).
    /// Alerting rule: any non-zero value in a rolling 1-hour window warrants operations intervention.
    /// </summary>
    public static readonly Counter<long> AiRapidCyclingCounter =
        Meter.CreateCounter<long>(
            "ai.circuit_rapid_cycling",
            unit: "{events}",
            description: "AI gateway rapid circuit-cycling events — ≥3 trips/hour (US_053, Edge Case 1).");

    /// <summary>
    /// Histogram of AI gateway request duration in milliseconds (AIR-006: p95 ≤ 2500ms).
    /// Tagged with <c>circuit_state</c> and <c>outcome</c> ("success" / "fallback" / "timeout").
    /// </summary>
    public static readonly Histogram<long> AiRequestDurationHistogram =
        Meter.CreateHistogram<long>(
            "ai.request_duration_ms",
            unit: "ms",
            description: "AI gateway request duration in milliseconds (AIR-006 SLO: p95 ≤ 2500ms).");

    /// <summary>
    /// Incremented each time an AI gateway request is cancelled due to the configured timeout (AC-4).
    /// Tagged with <c>model</c> to identify which model is timing out.
    /// </summary>
    public static readonly Counter<long> AiTimeoutCounter =
        Meter.CreateCounter<long>(
            "ai.timeout_count",
            unit: "{timeouts}",
            description: "Total AI gateway request timeouts (US_053, AC-4).");
    // ── AI Audit Metrics (US_055, AIR-011) ────────────────────────────────────

    /// <summary>
    /// Incremented when an AI audit log write fails (both primary and outbox paths).
    /// Tagged with <c>reason</c> ("primary_write_failed" | "outbox_write_failed" | "max_retries_exhausted").
    /// Non-zero values in a rolling window warrant compliance operations intervention (Edge Case 1).
    /// </summary>
    public static readonly Counter<long> AuditWriteFailureCounter =
        Meter.CreateCounter<long>(
            "compliance.audit_write_failure",
            unit: "{failures}",
            description: "AI audit log write failures — primary and outbox path (US_055, Edge Case 1).");

    /// <summary>
    /// Incremented when an <c>AuditEvent</c> cannot be persisted and is moved to the dead-letter table,
    /// or when the dead-letter write itself fails (US_056, AC-2, Edge Case 1).
    /// Non-zero values in a rolling window warrant compliance operations investigation.
    /// </summary>
    public static readonly Counter<long> AuditRecordWriteFailureCounter =
        Meter.CreateCounter<long>(
            "compliance.audit_record_write_failure",
            unit: "{failures}",
            description: "Audit record channel write failures — dead-letter and critical loss (US_056, Edge Case 1).");
}
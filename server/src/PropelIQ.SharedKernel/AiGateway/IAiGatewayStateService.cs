using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// Tracks the AI gateway circuit breaker state in Redis and provides a fast
/// in-memory flag for per-request hot-path checks (US_053, Edge Case 1, Edge Case 2).
///
/// Redis keys:
/// <list type="bullet">
///   <item><c>ai:circuit:state</c> — current state string: <c>"closed"</c> / <c>"open"</c> / <c>"half-open"</c>.</item>
///   <item><c>ai:circuit:last_trip_at</c> — ISO 8601 UTC timestamp of the last circuit trip.</item>
///   <item><c>ai:circuit:trip_count:{yyyyMMddHH}</c> — hourly trip counter; expires after 1 hour.</item>
/// </list>
/// </summary>
public interface IAiGatewayStateService
{
    /// <summary>
    /// Returns <c>true</c> when the circuit is <c>open</c> or <c>half-open</c>.
    ///
    /// Uses an in-memory volatile flag for synchronous, zero-latency hot-path evaluation
    /// (called per request by <c>AiFallbackEnvelopeMiddleware</c> and by
    /// <c>LiteLlmGatewayClient.IsCircuitBreakerOpen</c>).
    ///
    /// Flag is updated synchronously by Polly <c>OnOpened</c> / <c>OnClosed</c> /
    /// <c>OnHalfOpened</c> callbacks — no Redis round-trip needed on the hot path.
    /// </summary>
    bool IsCircuitOpen();

    /// <summary>
    /// Returns the full circuit state from Redis including <see cref="AiGatewayStatusDto.LastTripAt"/>.
    /// Used by the <c>AiGatewayController</c> status endpoint (Edge Case 2).
    /// </summary>
    Task<AiGatewayStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the in-memory flag and writes the new state string to Redis.
    /// Called by Polly <c>OnClosed</c> and <c>OnHalfOpened</c> callbacks.
    /// </summary>
    Task SetStateAsync(string state, CancellationToken ct = default);

    /// <summary>
    /// Records a circuit trip:
    /// <list type="number">
    ///   <item>Sets in-memory flag and Redis state to <c>"open"</c>.</item>
    ///   <item>Persists <c>last_trip_at</c> timestamp.</item>
    ///   <item>Increments the hourly trip counter.</item>
    ///   <item>Emits <c>ai.circuit_rapid_cycling</c> OTel metric when ≥ 3 trips/hour (Edge Case 1).</item>
    /// </list>
    /// Called by the Polly circuit-breaker <c>OnOpened</c> callback.
    /// </summary>
    Task RecordTripAsync(CancellationToken ct = default);
}

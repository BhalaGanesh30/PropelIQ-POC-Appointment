using Microsoft.Extensions.Logging;
using PropelIQ.SharedKernel.AiGateway.Models;
using PropelIQ.SharedKernel.Caching;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// Redis-backed implementation of <see cref="IAiGatewayStateService"/> (US_053, AC-2, AC-3).
///
/// Maintains a volatile in-memory boolean for zero-latency per-request checks, updated
/// synchronously by Polly circuit-breaker callbacks. Redis provides durable state for the
/// status endpoint and audit trail (Edge Case 2).
///
/// Redis unavailability is tolerated: ICacheService.SetAsync/GetAsync never throw
/// (RedisCacheService swallows exceptions via its own circuit-breaker). The in-memory
/// flag remains the definitive hot-path source.
///
/// Registered as a singleton so the in-memory flag is shared across all requests
/// within a process lifetime.
/// </summary>
public sealed class AiGatewayStateService : IAiGatewayStateService
{
    // ── Redis key constants ────────────────────────────────────────────────────
    private const string StateKey       = "ai:circuit:state";
    private const string LastTripAtKey  = "ai:circuit:last_trip_at";

    // Hourly trip counter key; format yyyyMMddHH ensures one bucket per clock-hour (UTC).
    private static string TripCountKey  => $"ai:circuit:trip_count:{DateTimeOffset.UtcNow:yyyyMMddHH}";

    // ── Rapid-cycling threshold (Edge Case 1) ──────────────────────────────────
    private const int RapidCyclingThreshold = 3;

    private readonly ICacheService _cache;
    private readonly ILogger<AiGatewayStateService> _logger;

    /// <summary>
    /// In-memory volatile flag; true when circuit is <c>open</c> or <c>half-open</c>.
    /// Written by Polly callbacks (potentially from background threads); volatile
    /// prevents CPU-level caching inconsistencies across cores.
    /// </summary>
    private volatile bool _isOpen;

    public AiGatewayStateService(
        ICacheService cache,
        ILogger<AiGatewayStateService> logger)
    {
        _cache  = cache;
        _logger = logger;
        // In-memory state starts closed. Polly will call RecordTripAsync / SetStateAsync
        // if the circuit has a reason to open — no Redis read needed at startup.
        _isOpen = false;
    }

    /// <inheritdoc />
    public bool IsCircuitOpen() => _isOpen;

    /// <inheritdoc />
    public async Task<AiGatewayStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        // Read from Redis for persistent state (includes last_trip_at across restarts).
        var state      = await _cache.GetAsync<string>(StateKey, ct) ?? "closed";
        var lastTripAt = await _cache.GetAsync<string>(LastTripAtKey, ct);

        DateTimeOffset? parsedTripAt = lastTripAt is not null
            && DateTimeOffset.TryParse(lastTripAt, out var dt)
            ? dt
            : null;

        bool fallbackActive = state is "open" or "half-open";
        return new AiGatewayStatusDto(state, fallbackActive, parsedTripAt);
    }

    /// <inheritdoc />
    public async Task SetStateAsync(string state, CancellationToken ct = default)
    {
        _isOpen = state is "open" or "half-open";

        await _cache.SetAsync(StateKey, state, ttl: null, ct);

        _logger.LogInformation(
            "AI gateway circuit state set to '{State}' (in-memory flag: {IsOpen}).",
            state,
            _isOpen);
    }

    /// <inheritdoc />
    public async Task RecordTripAsync(CancellationToken ct = default)
    {
        _isOpen = true;

        var now = DateTimeOffset.UtcNow;

        // Persist state and trip timestamp (TTL = null → no expiry; circuit state must be
        // explicitly closed by Polly OnClosed callback).
        await _cache.SetAsync(StateKey, "open", ttl: null, ct);
        await _cache.SetAsync(LastTripAtKey, now.ToString("O"), ttl: null, ct);

        // Increment the hourly trip counter (expires after 1 hour; Edge Case 1).
        var countStr = await _cache.GetAsync<string>(TripCountKey, ct);
        var count    = countStr is not null && int.TryParse(countStr, out var parsed) ? parsed : 0;
        count++;
        await _cache.SetAsync(TripCountKey, count.ToString(), ttl: TimeSpan.FromHours(1), ct);

        // OTel: total trip counter (tagged for operations dashboard).
        DiagnosticsConfig.AiCircuitTripCounter.Add(
            1,
            new KeyValuePair<string, object?>("circuit.state", "open"));

        // OTel: rapid cycling alert (Edge Case 1) — emitted when circuit trips ≥ 3 times/hour.
        if (count >= RapidCyclingThreshold)
        {
            DiagnosticsConfig.AiRapidCyclingCounter.Add(
                1,
                new KeyValuePair<string, object?>("hour", now.ToString("yyyyMMddHH")));

            _logger.LogWarning(
                "AI gateway circuit rapid cycling detected: {Count} trips in the current hour " +
                "(threshold: {Threshold}). Operations alert emitted via ai.circuit_rapid_cycling counter (Edge Case 1).",
                count,
                RapidCyclingThreshold);
        }

        _logger.LogWarning(
            "AI gateway circuit breaker tripped (trip #{Count} this hour). " +
            "LastTripAt: {LastTripAt}. Fallback active.",
            count,
            now);
    }
}

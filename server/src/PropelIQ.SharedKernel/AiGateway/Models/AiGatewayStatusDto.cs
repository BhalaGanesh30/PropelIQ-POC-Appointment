namespace PropelIQ.SharedKernel.AiGateway.Models;

/// <summary>
/// Circuit breaker status response returned by <c>GET /api/v1/ai-gateway/status</c>
/// and by <see cref="IAiGatewayStateService.GetStatusAsync"/> (US_053, Edge Case 2).
///
/// Maps directly to the Angular <c>AiGatewayStatusDto</c> frontend interface; property
/// names serialise to camelCase via ASP.NET Core's default <c>JsonNamingPolicy</c>.
/// </summary>
public sealed record AiGatewayStatusDto(
    /// <summary>Current circuit state: <c>"closed"</c>, <c>"open"</c>, or <c>"half-open"</c>.</summary>
    string CircuitState,
    /// <summary>True when circuit is <c>open</c> or <c>half-open</c>; mirrors FE fallback logic (AC-2, AC-3).</summary>
    bool FallbackActive,
    /// <summary>UTC timestamp of the most recent circuit trip; null if the circuit has never opened.</summary>
    DateTimeOffset? LastTripAt);

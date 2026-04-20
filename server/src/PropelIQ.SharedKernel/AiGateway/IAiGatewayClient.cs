using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// Abstraction for the AI gateway (LiteLLM proxy).
/// Callers send a <see cref="ChatCompletionRequest"/> and receive a nullable
/// <see cref="ChatCompletionResponse"/>. A null return indicates that the circuit
/// breaker is open or the request was unauthorised — callers should fall back to
/// the deterministic manual coding flow (AIR-005, AC-2).
/// </summary>
public interface IAiGatewayClient
{
    /// <summary>
    /// Sends a chat completion request through the AI gateway.
    /// Returns null when the circuit breaker is open or HTTP 401 is received,
    /// so the caller can activate the deterministic fallback path.
    /// </summary>
    Task<ChatCompletionResponse?> GetCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the Polly circuit breaker is currently open (AC-2).
    /// Callers can inspect this flag to skip the gateway and route directly
    /// to the deterministic fallback without incurring a failed HTTP call.
    /// </summary>
    bool IsCircuitBreakerOpen { get; }
}

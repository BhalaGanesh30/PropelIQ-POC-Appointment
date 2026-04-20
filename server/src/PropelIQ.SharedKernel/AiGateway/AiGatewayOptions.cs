using System.ComponentModel.DataAnnotations;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// Strongly-typed configuration options for the AI gateway (LiteLLM proxy).
/// Registered with ValidateDataAnnotations() and ValidateOnStart() so a missing
/// or malformed AiGateway config section causes the application to fail at startup
/// with a descriptive validation error (edge case: configuration validation).
/// </summary>
public sealed class AiGatewayOptions
{
    public const string SectionName = "AiGateway";

    /// <summary>Base URL of the LiteLLM proxy. Defaults to the local dev port.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:4000";

    /// <summary>
    /// LiteLLM master key passed as Bearer token on every request.
    /// Maps to LITELLM_MASTER_KEY in the Docker Compose stack.
    /// </summary>
    [Required, MinLength(1)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Default model alias used when callers do not specify a model.</summary>
    [Required, MinLength(1)]
    public string DefaultModel { get; set; } = "coding-suggestion";

    /// <summary>Maximum Polly retry attempts before the exception is propagated.</summary>
    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>Per-request timeout in seconds applied to the underlying HttpClient.</summary>
    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Number of consecutive failures required to open the circuit breaker.
    /// AC-4: circuit opens after this threshold is reached.
    /// </summary>
    [Range(1, 20)]
    public int CircuitBreakerFailureThreshold { get; set; } = 3;

    /// <summary>Duration in seconds the circuit stays open before entering half-open.</summary>
    [Range(10, 300)]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}

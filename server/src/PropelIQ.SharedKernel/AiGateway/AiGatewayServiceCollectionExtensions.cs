using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// IServiceCollection extension that registers the AI gateway typed HttpClient
/// with Polly v8 retry (exponential backoff) and circuit-breaker resilience strategies.
/// Call <see cref="AddAiGateway"/> from the API composition root (Program.cs).
/// </summary>
public static class AiGatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAiGatewayClient"/> with:
    /// <list type="bullet">
    ///   <item>Strongly-typed options validated at startup (ValidateOnStart).</item>
    ///   <item>Typed HttpClient configured with base URL, Bearer token, and timeout.</item>
    ///   <item>Polly v8 retry — exponential backoff + jitter up to MaxRetries (AC-4).</item>
    ///   <item>Polly v8 circuit breaker — opens after CircuitBreakerFailureThreshold
    ///         failures; stays open for CircuitBreakerDurationSeconds (AC-2, AC-4).</item>
    /// </list>
    /// Policy order: circuit breaker (outermost) → retry (inner) → HTTP call.
    /// When circuit is open, <see cref="BrokenCircuitException"/> is thrown immediately
    /// without consuming retry budget.
    /// </summary>
    public static IServiceCollection AddAiGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options registration with startup validation (edge case: malformed config).
        services.AddOptions<AiGatewayOptions>()
            .BindConfiguration(AiGatewayOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Typed HttpClient with Polly v8 resilience pipeline applied as delegating handler.
        services.AddHttpClient<IAiGatewayClient, LiteLlmGatewayClient>(
            (serviceProvider, httpClient) =>
            {
                var opts = serviceProvider
                    .GetRequiredService<IOptions<AiGatewayOptions>>().Value;

                httpClient.BaseAddress = new Uri(opts.BaseUrl);
                // Master key auth — requests without a valid key receive HTTP 401 (edge case).
                httpClient.DefaultRequestHeaders.Add(
                    "Authorization", $"Bearer {opts.ApiKey}");
                // Hard client timeout slightly above the per-request policy timeout.
                httpClient.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds + 5);
            })
            .AddResilienceHandler("ai-gateway", (pipeline, context) =>
            {
                var opts = context.ServiceProvider
                    .GetRequiredService<IOptions<AiGatewayOptions>>().Value;
                var logger = context.ServiceProvider
                    .GetRequiredService<ILogger<LiteLlmGatewayClient>>();

                // AC-2/AC-4: circuit breaker is outermost — when open, throws
                // BrokenCircuitException immediately without consuming retry budget.
                pipeline.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    // Evaluate failures over a window equal to the break duration.
                    SamplingDuration = TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds * 2),
                    MinimumThroughput = opts.CircuitBreakerFailureThreshold,
                    FailureRatio = 0.5,
                    BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    OnOpened = args =>
                    {
                        logger.LogWarning(
                            "AI gateway circuit breaker OPENED for {Duration}s.",
                            args.BreakDuration.TotalSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation(
                            "AI gateway circuit breaker RESET — traffic restored.");
                        return ValueTask.CompletedTask;
                    },
                });

                // AC-4: retry with exponential backoff + jitter is inner to circuit breaker.
                // Each final failure after exhausting retries is counted by the circuit breaker.
                pipeline.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = opts.MaxRetries,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(1),
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            "AI gateway retry {Attempt}/{Max} after {Delay}s. Status: {Status}",
                            args.AttemptNumber + 1,
                            opts.MaxRetries,
                            args.RetryDelay.TotalSeconds,
                            args.Outcome.Result?.StatusCode);
                        return ValueTask.CompletedTask;
                    },
                });
            });

        return services;
    }
}

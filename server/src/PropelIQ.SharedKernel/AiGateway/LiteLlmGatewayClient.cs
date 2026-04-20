using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Polly.CircuitBreaker;
using PropelIQ.SharedKernel.AiGateway.Models;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.SharedKernel.AiGateway;

/// <summary>
/// HttpClient-based implementation of <see cref="IAiGatewayClient"/> that
/// communicates with the LiteLLM proxy over its OpenAI-compatible HTTP API.
///
/// Reliability (AC-4): Polly retry and circuit-breaker policies are applied at
/// the HttpClientFactory handler level (see AiGatewayServiceCollectionExtensions).
/// Observability (AC-3): every call opens an OTel child span recording latency,
/// token usage, and model name linked to the parent HTTP request trace.
/// Fallback (AC-2): BrokenCircuitException is caught and converted to a null
/// return — callers activate the deterministic manual coding flow (AIR-005).
/// </summary>
public sealed class LiteLlmGatewayClient : IAiGatewayClient
{
    // Snake_case serialisation matches the OpenAI API JSON contract.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly AiGatewayOptions _options;
    private readonly ILogger<LiteLlmGatewayClient> _logger;

    /// <inheritdoc />
    public bool IsCircuitBreakerOpen { get; private set; }

    public LiteLlmGatewayClient(
        HttpClient httpClient,
        IOptions<AiGatewayOptions> options,
        ILogger<LiteLlmGatewayClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse?> GetCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // AC-3: open OTel child span linked to the parent request trace.
        using var activity = DiagnosticsConfig.ActivitySource.StartActivity(
            "AiGateway.ChatCompletion",
            ActivityKind.Client);

        activity?.SetTag("ai.gateway.model", request.Model);
        activity?.SetTag("ai.gateway.provider", "litellm");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var httpResponse = await _httpClient.PostAsJsonAsync(
                "/chat/completions",
                request,
                JsonOptions,
                cancellationToken);

            stopwatch.Stop();

            // Edge case: master key rejected — log, record in span, return null.
            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(
                    "AI gateway returned 401 Unauthorized for model {Model}. " +
                    "Verify LITELLM_MASTER_KEY is set correctly.",
                    request.Model);

                activity?.SetTag("ai.gateway.status", "unauthorized");
                activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Error, "Unauthorized");
                return null;
            }

            httpResponse.EnsureSuccessStatusCode();

            var result = await httpResponse.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken);

            // AC-3: record latency, token usage, and model name in the span.
            activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("ai.gateway.model_returned", result?.Model);
            activity?.SetTag("ai.gateway.prompt_tokens", result?.Usage.PromptTokens);
            activity?.SetTag("ai.gateway.completion_tokens", result?.Usage.CompletionTokens);
            activity?.SetTag("ai.gateway.total_tokens", result?.Usage.TotalTokens);
            activity?.SetTag("ai.gateway.status", "success");

            DiagnosticsConfig.ExternalCallCounter.Add(1,
                new KeyValuePair<string, object?>("provider", "litellm"),
                new KeyValuePair<string, object?>("model", request.Model),
                new KeyValuePair<string, object?>("status", "success"));

            return result;
        }
        catch (BrokenCircuitException)
        {
            // AC-2: circuit is open — return null so callers activate the
            // deterministic manual coding fallback (AIR-005) without an unhandled exception.
            stopwatch.Stop();
            IsCircuitBreakerOpen = true;

            _logger.LogWarning(
                "AI gateway circuit breaker is open for model {Model}. " +
                "Falling back to deterministic manual coding flow (AIR-005).",
                request.Model);

            activity?.SetTag("ai.gateway.status", "circuit_breaker_open");
            activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, "CircuitBreakerOpen");

            DiagnosticsConfig.ExternalCallCounter.Add(1,
                new KeyValuePair<string, object?>("provider", "litellm"),
                new KeyValuePair<string, object?>("model", request.Model),
                new KeyValuePair<string, object?>("status", "circuit_breaker_open"));

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Transient HTTP error or timeout: log, record in span, and re-throw
            // so the Polly retry policy (applied at the handler level) can fire.
            stopwatch.Stop();

            _logger.LogError(ex,
                "AI gateway request failed for model {Model} after {ElapsedMs}ms.",
                request.Model,
                stopwatch.ElapsedMilliseconds);

            activity?.SetTag("ai.gateway.status", "error");
            activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            DiagnosticsConfig.ErrorCounter.Add(1,
                new KeyValuePair<string, object?>("source", "ai_gateway"),
                new KeyValuePair<string, object?>("model", request.Model));

            throw;
        }
    }
}

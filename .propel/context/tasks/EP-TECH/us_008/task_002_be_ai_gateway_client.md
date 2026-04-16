# Task - TASK_002

## Requirement Reference

- User Story: us_008
- Story Location: .propel/context/tasks/EP-TECH/us_008/us_008.md
- Acceptance Criteria:
  - AC-1: Given the AI gateway is configured, When a coding suggestion request is sent through the gateway, Then the request is routed to the configured model provider and a structured response is returned.
  - AC-2: Given the gateway is active, When the model provider returns an error or times out, Then the circuit breaker opens and the system falls back to the deterministic manual coding flow without throwing an unhandled exception.
  - AC-3: Given a request is processed by the gateway, When the response arrives, Then latency, token usage, and model name are emitted as OpenTelemetry spans linked to the parent request trace.
  - AC-4: Given multiple model providers are configured, When the primary provider is unavailable, Then the gateway retries with exponential backoff up to the configured retry limit before activating the circuit breaker.
- Edge Case:
  - What happens if the gateway configuration file is malformed? The application fails to start with a descriptive configuration validation error.
  - How does the system handle gateway requests without a valid API key? Gateway returns HTTP 401; no model request is made; the error is logged.

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | N/A | N/A |
| Library | Microsoft.Extensions.Http.Polly | 8.x |
| Library | Polly | 8.x |
| Library | OpenTelemetry.Api | 1.x (latest stable) |
| Library | System.Net.Http.Json | 8.x |
| AI/ML | Azure OpenAI (via LiteLLM proxy) | 2026 APIs |
| Vector Store | N/A | N/A |
| AI Gateway | LiteLLM-compatible gateway | latest stable |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-005, AIR-006, AIR-011 |
| **AI Pattern** | Hybrid |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | Azure OpenAI (via LiteLLM proxy) |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the C# AI gateway client abstraction layer that communicates with the LiteLLM proxy (from task_001) via its OpenAI-compatible HTTP API. The client is built as `IAiGatewayClient` in the SharedKernel module, using `HttpClient` with typed configuration. A Polly circuit breaker policy wraps all gateway calls: on provider errors or timeouts, after the configured failure threshold is reached, the circuit opens and the system falls back to the deterministic manual coding flow (AIR-005) without throwing unhandled exceptions. Every gateway request creates an OpenTelemetry child span under the parent HTTP request trace, recording latency, token usage (prompt + completion tokens), and model name as span attributes. Configuration is validated at startup using `IOptionsMonitor<T>` with `ValidateOnStart()` so a malformed or missing gateway config section causes the application to fail with a descriptive error. API key validation ensures unauthorized requests are logged and rejected gracefully.

## Dependent Tasks

- task_001_infra_litellm_proxy (requires LiteLLM proxy running on port 4000)
- US_002 tasks (requires ASP.NET Core solution structure)
- US_007 task_001 (requires OpenTelemetry instrumentation with DiagnosticsConfig)

## Impacted Components

- New: `server/src/SharedKernel/AiGateway/IAiGatewayClient.cs` (abstraction interface)
- New: `server/src/SharedKernel/AiGateway/LiteLlmGatewayClient.cs` (HttpClient-based implementation)
- New: `server/src/SharedKernel/AiGateway/AiGatewayOptions.cs` (strongly-typed configuration with validation)
- New: `server/src/SharedKernel/AiGateway/Models/ChatCompletionRequest.cs` (request DTO)
- New: `server/src/SharedKernel/AiGateway/Models/ChatCompletionResponse.cs` (response DTO)
- New: `server/src/SharedKernel/AiGateway/AiGatewayServiceCollectionExtensions.cs` (DI registration)
- Modify: `server/src/PropelIQ.Api/Program.cs` (register AI gateway services)
- Modify: `server/src/SharedKernel/SharedKernel.csproj` (add Polly NuGet packages)
- Modify: `server/src/PropelIQ.Api/appsettings.json` (add AiGateway configuration section)

## Implementation Plan

1. **Create `AiGatewayOptions.cs`** as a strongly-typed options class with `ValidateDataAnnotations` and `ValidateOnStart()` to catch malformed configuration at application startup (edge case):

```csharp
using System.ComponentModel.DataAnnotations;

namespace PropelIQ.SharedKernel.AiGateway;

public sealed class AiGatewayOptions
{
    public const string SectionName = "AiGateway";

    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:4000";

    [Required, MinLength(1)]
    public string ApiKey { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string DefaultModel { get; set; } = "coding-suggestion";

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(1, 20)]
    public int CircuitBreakerFailureThreshold { get; set; } = 3;

    [Range(10, 300)]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
```

Registered in DI with validation:
```csharp
services.AddOptions<AiGatewayOptions>()
    .BindConfiguration(AiGatewayOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

2. **Create request/response DTOs** matching the OpenAI-compatible API contract exposed by LiteLLM:

```csharp
// ChatCompletionRequest.cs
namespace PropelIQ.SharedKernel.AiGateway.Models;

public sealed record ChatCompletionRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int? MaxTokens { get; init; }
}

public sealed record ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

// ChatCompletionResponse.cs
public sealed record ChatCompletionResponse
{
    public required string Id { get; init; }
    public required string Model { get; init; }
    public required IReadOnlyList<Choice> Choices { get; init; }
    public required UsageInfo Usage { get; init; }
}

public sealed record Choice
{
    public required ChatMessage Message { get; init; }
    public string? FinishReason { get; init; }
}

public sealed record UsageInfo
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
}
```

3. **Create `IAiGatewayClient.cs`** interface defining the gateway abstraction contract:

```csharp
namespace PropelIQ.SharedKernel.AiGateway;

public interface IAiGatewayClient
{
    Task<ChatCompletionResponse?> GetCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    bool IsCircuitBreakerOpen { get; }
}
```

The nullable return type allows callers to detect circuit breaker fallback (returns `null` when circuit is open → caller falls back to deterministic flow per AIR-005).

4. **Create `LiteLlmGatewayClient.cs`** implementation with OpenTelemetry child spans and Polly circuit breaker:

```csharp
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.SharedKernel.AiGateway;

public sealed class LiteLlmGatewayClient : IAiGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly AiGatewayOptions _options;
    private readonly ILogger<LiteLlmGatewayClient> _logger;

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

    public async Task<ChatCompletionResponse?> GetCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // AC-3: Create OTel child span linked to parent request trace
        using var activity = DiagnosticsConfig.ActivitySource.StartActivity(
            "AiGateway.ChatCompletion",
            ActivityKind.Client);

        activity?.SetTag("ai.gateway.model", request.Model);
        activity?.SetTag("ai.gateway.provider", "litellm");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/chat/completions",
                request,
                cancellationToken);

            stopwatch.Stop();

            // Edge case: API key rejected
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(
                    "AI gateway returned 401 Unauthorized for model {Model}",
                    request.Model);
                activity?.SetTag("ai.gateway.status", "unauthorized");
                activity?.SetStatus(ActivityStatusCode.Error, "Unauthorized");
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);

            // AC-3: Record latency, token usage, model name
            activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("ai.gateway.model_returned", result?.Model);
            activity?.SetTag("ai.gateway.prompt_tokens", result?.Usage.PromptTokens);
            activity?.SetTag("ai.gateway.completion_tokens", result?.Usage.CompletionTokens);
            activity?.SetTag("ai.gateway.total_tokens", result?.Usage.TotalTokens);
            activity?.SetTag("ai.gateway.status", "success");

            DiagnosticsConfig.ExternalCallCounter.Add(1,
                new("provider", "litellm"),
                new("model", request.Model),
                new("status", "success"));

            return result;
        }
        catch (BrokenCircuitException)
        {
            // AC-2: Circuit breaker is open — fall back to deterministic flow
            stopwatch.Stop();
            IsCircuitBreakerOpen = true;

            _logger.LogWarning(
                "AI gateway circuit breaker is open for model {Model}. " +
                "Falling back to deterministic manual coding flow.",
                request.Model);

            activity?.SetTag("ai.gateway.status", "circuit_breaker_open");
            activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, "CircuitBreakerOpen");

            DiagnosticsConfig.ExternalCallCounter.Add(1,
                new("provider", "litellm"),
                new("model", request.Model),
                new("status", "circuit_breaker_open"));

            return null; // Caller uses null to trigger deterministic fallback
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "AI gateway request failed for model {Model} after {ElapsedMs}ms",
                request.Model,
                stopwatch.ElapsedMilliseconds);

            activity?.SetTag("ai.gateway.status", "error");
            activity?.SetTag("ai.gateway.latency_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            DiagnosticsConfig.ErrorCounter.Add(1,
                new("source", "ai_gateway"),
                new("model", request.Model));

            throw; // Let Polly retry policy handle
        }
    }
}
```

5. **Create `AiGatewayServiceCollectionExtensions.cs`** to register the typed `HttpClient` with Polly circuit breaker and retry policies:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;

namespace PropelIQ.SharedKernel.AiGateway;

public static class AiGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddAiGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options with startup validation (edge case: malformed config)
        services.AddOptions<AiGatewayOptions>()
            .BindConfiguration(AiGatewayOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Build Polly policies from options
        services.AddHttpClient<IAiGatewayClient, LiteLlmGatewayClient>(
            (serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiGatewayOptions>>().Value;
                httpClient.BaseAddress = new Uri(options.BaseUrl);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
                httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddPolicyHandler((serviceProvider, _) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiGatewayOptions>>().Value;

                // AC-4: Retry with exponential backoff
                var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(
                        retryCount: options.MaxRetries,
                        sleepDurationProvider: attempt =>
                            TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                        onRetry: (outcome, delay, attempt, _) =>
                        {
                            var logger = serviceProvider
                                .GetRequiredService<ILogger<LiteLlmGatewayClient>>();
                            logger.LogWarning(
                                "AI gateway retry {Attempt}/{MaxRetries} after {Delay}s. " +
                                "Status: {StatusCode}",
                                attempt, options.MaxRetries, delay.TotalSeconds,
                                outcome.Result?.StatusCode);
                        });

                return retryPolicy;
            })
            .AddPolicyHandler((serviceProvider, _) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiGatewayOptions>>().Value;

                // AC-2/AC-4: Circuit breaker opens after failure threshold
                var circuitBreakerPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking:
                            options.CircuitBreakerFailureThreshold,
                        durationOfBreak:
                            TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds),
                        onBreak: (outcome, breakDuration) =>
                        {
                            var logger = serviceProvider
                                .GetRequiredService<ILogger<LiteLlmGatewayClient>>();
                            logger.LogWarning(
                                "AI gateway circuit breaker opened for {Duration}s. " +
                                "Last status: {StatusCode}",
                                breakDuration.TotalSeconds,
                                outcome.Result?.StatusCode);
                        },
                        onReset: () =>
                        {
                            var logger = serviceProvider
                                .GetRequiredService<ILogger<LiteLlmGatewayClient>>();
                            logger.LogInformation("AI gateway circuit breaker reset.");
                        });

                return circuitBreakerPolicy;
            });

        return services;
    }
}
```

6. **Register in `Program.cs`**:

```csharp
builder.Services.AddAiGateway(builder.Configuration);
```

7. **Add configuration section** to `appsettings.json`:

```json
{
  "AiGateway": {
    "BaseUrl": "http://localhost:4000",
    "ApiKey": "",
    "DefaultModel": "coding-suggestion",
    "MaxRetries": 3,
    "TimeoutSeconds": 10,
    "CircuitBreakerFailureThreshold": 3,
    "CircuitBreakerDurationSeconds": 30
  }
}
```

For Docker Compose environments, the `BaseUrl` becomes `http://litellm-gateway:4000` and the `ApiKey` maps to the `LITELLM_MASTER_KEY` set in task_001.

8. **Add NuGet packages** to `SharedKernel.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="8.*" />
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       ├── PropelIQ.Api/
│       │   ├── PropelIQ.Api.csproj
│       │   ├── Program.cs
│       │   └── appsettings.json
│       ├── SharedKernel/
│       │   ├── SharedKernel.csproj
│       │   └── Observability/    (from US_007 task_001)
│       │       ├── DiagnosticsConfig.cs
│       │       ├── TelemetryServiceCollectionExtensions.cs
│       │       ├── CorrelationIdMiddleware.cs
│       │       └── CardinalityHasher.cs
│       └── ...
├── docker-compose.yml
├── infra/
│   └── litellm/
│       └── config.yaml          (from task_001)
└── .env.example
```

> Placeholder: Update on execution based on dependent task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/SharedKernel/AiGateway/IAiGatewayClient.cs | Gateway abstraction interface with GetCompletionAsync and IsCircuitBreakerOpen |
| CREATE | server/src/SharedKernel/AiGateway/LiteLlmGatewayClient.cs | HttpClient-based implementation with OTel spans and circuit breaker handling |
| CREATE | server/src/SharedKernel/AiGateway/AiGatewayOptions.cs | Strongly-typed options with DataAnnotations and ValidateOnStart |
| CREATE | server/src/SharedKernel/AiGateway/Models/ChatCompletionRequest.cs | Request DTO matching OpenAI chat completion format |
| CREATE | server/src/SharedKernel/AiGateway/Models/ChatCompletionResponse.cs | Response DTO with choices and usage info |
| CREATE | server/src/SharedKernel/AiGateway/AiGatewayServiceCollectionExtensions.cs | DI registration with typed HttpClient, Polly retry, and circuit breaker |
| MODIFY | server/src/SharedKernel/SharedKernel.csproj | Add Microsoft.Extensions.Http.Polly and Options.DataAnnotations packages |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Register AddAiGateway services |
| MODIFY | server/src/PropelIQ.Api/appsettings.json | Add AiGateway configuration section |

## External References

- Polly circuit breaker (.NET 8): https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern
- Polly retry with HttpClientFactory: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
- IOptions validation on startup: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options#options-validation
- HttpClientFactory typed clients: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#typed-clients
- OpenTelemetry .NET Activity API: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/getting-started-aspnetcore/README.md
- LiteLLM OpenAI-compatible API: https://docs.litellm.ai/docs/proxy/user_keys
- System.Text.Json source generation: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation

## Build Commands

```bash
# Restore and build
dotnet restore server/PropelIQ.sln
dotnet build server/PropelIQ.sln --configuration Release

# Run API (ensure LiteLLM proxy is running via docker compose)
dotnet run --project server/src/PropelIQ.Api

# Verify startup validation (intentionally break config to test edge case)
# Remove "BaseUrl" from appsettings.json -> app should fail to start with validation error
```

## Implementation Validation Strategy

- [ ] Application starts successfully with valid AiGateway configuration section
- [ ] Application fails to start with descriptive validation error when AiGateway config is malformed or missing (edge case)
- [ ] `GetCompletionAsync` sends request to LiteLLM proxy and returns structured `ChatCompletionResponse` (AC-1)
- [ ] Circuit breaker opens after configured failure threshold and `GetCompletionAsync` returns `null` for deterministic fallback (AC-2)
- [ ] OTel child span records `ai.gateway.latency_ms`, `ai.gateway.prompt_tokens`, `ai.gateway.completion_tokens`, and `ai.gateway.model_returned` (AC-3)
- [ ] Polly retry policy retries with exponential backoff up to `MaxRetries` before circuit breaker activates (AC-4)
- [ ] Unauthorized requests (invalid API key) return `null` with warning log and 401 status recorded in span (edge case)
- [ ] No unhandled exceptions propagate from gateway failures

## Implementation Checklist

- [ ] Create `AiGatewayOptions.cs` with `[Required]` annotations, `ValidateDataAnnotations()`, and `ValidateOnStart()` for startup config validation
- [ ] Create `ChatCompletionRequest.cs` and `ChatCompletionResponse.cs` DTOs matching OpenAI chat completion format
- [ ] Create `IAiGatewayClient.cs` interface with `GetCompletionAsync` and `IsCircuitBreakerOpen` contract
- [ ] Create `LiteLlmGatewayClient.cs` with HttpClient calls, OTel child spans recording latency/tokens/model, and circuit breaker fallback returning `null`
- [ ] Create `AiGatewayServiceCollectionExtensions.cs` with typed HttpClient, Polly retry (exponential backoff), and circuit breaker policies
- [ ] Add `Microsoft.Extensions.Http.Polly` and `Microsoft.Extensions.Options.DataAnnotations` to `SharedKernel.csproj`
- [ ] Register `AddAiGateway` in `Program.cs` and add `AiGateway` section to `appsettings.json`
- [ ] Handle HTTP 401 gracefully: return `null`, log warning, record in OTel span without throwing

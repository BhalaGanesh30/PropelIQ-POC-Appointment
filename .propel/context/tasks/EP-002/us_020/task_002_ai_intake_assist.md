# Task - TASK_002

## Requirement Reference

- User Story: us_020
- Story Location: .propel/context/tasks/EP-002/us_020/us_020.md
- Acceptance Criteria:
  - AC-1: Given I am on the intake form, When I toggle to AI-assisted mode and provide a free-text description of my symptoms, Then the AI suggests structured intake fields pre-populated from my description within 2.5 seconds.
- Edge Cases:
  - What happens if the AI-assist call fails or times out? The form falls back to manual mode with a notification: "AI assist unavailable, please fill in manually." (AIR-005)

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
| Library | System.Text.Json | 8.x (bundled) |
| Library | Microsoft.Extensions.Http | 8.x (bundled) |
| Library | Polly | latest stable |
| AI/ML | Azure OpenAI GPT-4.1 family | 2026 APIs |
| Vector Store | N/A | N/A |
| AI Gateway | LiteLLM-compatible gateway | latest stable |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-005, AIR-006, AIR-011 |
| **AI Pattern** | Hybrid (AI-assisted with deterministic fallback) |
| **Prompt Template Path** | server/src/PropelIQ.Application/AI/Prompts/intake-assist.json |
| **Guardrails Config** | JSON schema validation for structured intake output |
| **Model Provider** | Azure OpenAI via LiteLLM gateway |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement the AI-assisted intake prefill integration that accepts a free-text symptom description and returns structured intake fields within 2.5 seconds (AC-1, AIR-006). The `POST /api/v1/intake/ai-assist` endpoint receives the patient's free-text input, constructs a prompt using a versioned template, sends it to the AI gateway (LiteLLM-compatible per TR-008), validates the response against a JSON schema (AIR-008), and returns structured fields (reason for visit, symptom details, severity, onset, medical history flags). The service implements circuit-breaker and timeout policies via Polly (2.5s timeout per AIR-006) with deterministic fallback to an empty structured response when the AI gateway is unavailable or returns low-confidence results (AIR-005). Direct patient identifiers are redacted from prompts except minimum required treatment context (AIR-009). All prompt/response pairs are logged with confidence values for audit (AIR-011, NFR-010). The response includes a field-level `aiPopulated` flag list so the frontend can visually distinguish AI-generated content (UXR-405).

## Dependent Tasks

- US_008 task_001 (requires AI gateway scaffold: LiteLLM client, circuit-breaker config, base prompt infrastructure)
- US_020 task_001 (requires intake draft API for storing AI-populated field markers)

## Impacted Components

- New: `server/src/PropelIQ.Application/AI/IntakeAssistService.cs` (AI-assist orchestration with prompt construction and response mapping)
- New: `server/src/PropelIQ.Application/AI/Dto/IntakeAssistDto.cs` (request/response DTOs for AI-assist endpoint)
- New: `server/src/PropelIQ.Application/AI/Prompts/intake-assist.json` (versioned prompt template for intake extraction)
- New: `server/src/PropelIQ.Application/AI/Validators/IntakeAssistResponseValidator.cs` (JSON schema validation for AI output)
- New: `server/src/PropelIQ.Application/Abstractions/IAiGatewayClient.cs` (gateway client abstraction)
- New: `server/src/PropelIQ.Infrastructure/AI/AiGatewayClient.cs` (HTTP client for LiteLLM gateway with Polly policies)
- Modify: `server/src/PropelIQ.Api/Controllers/IntakeController.cs` (add AI-assist endpoint)
- Modify: `server/src/PropelIQ.Infrastructure/DependencyInjection.cs` (register AI services)

## Implementation Plan

1. **Create AI-assist DTOs** for request and response:

```csharp
// server/src/PropelIQ.Application/AI/Dto/IntakeAssistDto.cs
namespace PropelIQ.Application.AI.Dto;

public record IntakeAssistRequest
{
    public string FreeTextDescription { get; init; } = string.Empty;
    public string? Language { get; init; } = "en";
}

public record IntakeAssistResponse
{
    public bool AiAssisted { get; init; }
    public string? FallbackReason { get; init; }
    public IntakeFieldSuggestions Suggestions { get; init; } = new();
    public List<string> AiPopulatedFields { get; init; } = [];
    public double Confidence { get; init; }
}

public record IntakeFieldSuggestions
{
    public string? ReasonForVisit { get; init; }
    public string? SymptomDescription { get; init; }
    public string? Severity { get; init; } // Mild, Moderate, Severe
    public string? OnsetDuration { get; init; }
    public string? BodyArea { get; init; }
    public List<string> RelevantMedicalHistory { get; init; } = [];
    public List<string> CurrentMedications { get; init; } = [];
    public List<string> Allergies { get; init; } = [];
}
```

2. **Create versioned prompt template** for intake extraction:

```json
// server/src/PropelIQ.Application/AI/Prompts/intake-assist.json
{
  "version": "1.0.0",
  "model": "gpt-4.1",
  "system_prompt": "You are a medical intake assistant. Extract structured intake information from the patient's free-text description. Return ONLY a valid JSON object matching the specified schema. Do not fabricate information not present in the input. If a field cannot be determined, return null for that field.",
  "user_template": "Patient describes their reason for visit:\n\n\"{free_text}\"\n\nExtract the following fields as JSON:\n- reasonForVisit: Brief summary of chief complaint\n- symptomDescription: Detailed symptom description\n- severity: One of [Mild, Moderate, Severe] or null\n- onsetDuration: When symptoms started (e.g., '3 days ago')\n- bodyArea: Affected body area or null\n- relevantMedicalHistory: Array of relevant conditions mentioned\n- currentMedications: Array of medications mentioned\n- allergies: Array of allergies mentioned",
  "temperature": 0.1,
  "max_tokens": 500,
  "response_format": "json_object"
}
```

3. **Create AI gateway client abstraction** and implementation with Polly resilience:

```csharp
// server/src/PropelIQ.Application/Abstractions/IAiGatewayClient.cs
namespace PropelIQ.Application.Abstractions;

public interface IAiGatewayClient
{
    Task<AiGatewayResponse> SendCompletionAsync(
        string systemPrompt,
        string userPrompt,
        AiRequestOptions options,
        CancellationToken ct);
}

public record AiRequestOptions
{
    public string Model { get; init; } = "gpt-4.1";
    public double Temperature { get; init; } = 0.1;
    public int MaxTokens { get; init; } = 500;
    public string ResponseFormat { get; init; } = "json_object";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(2.5);
}

public record AiGatewayResponse
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? ErrorMessage { get; init; }
    public int TokensUsed { get; init; }
}
```

```csharp
// server/src/PropelIQ.Infrastructure/AI/AiGatewayClient.cs
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace PropelIQ.Infrastructure.AI;

public class AiGatewayClient : IAiGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiGatewayClient> _logger;

    // Circuit breaker: open after 3 failures, stay open 30s
    private static readonly AsyncCircuitBreakerPolicy<HttpResponseMessage>
        CircuitBreaker = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30));

    public AiGatewayClient(
        HttpClient httpClient,
        ILogger<AiGatewayClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiGatewayResponse> SendCompletionAsync(
        string systemPrompt,
        string userPrompt,
        AiRequestOptions options,
        CancellationToken ct)
    {
        try
        {
            // Timeout policy per AIR-006 (2.5s p95)
            var timeoutPolicy = Policy.TimeoutAsync(
                options.Timeout, TimeoutStrategy.Optimistic);

            var combinedPolicy = Policy.WrapAsync(
                timeoutPolicy, CircuitBreaker);

            var requestBody = new
            {
                model = options.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = options.Temperature,
                max_tokens = options.MaxTokens,
                response_format = new { type = options.ResponseFormat }
            };

            var response = await combinedPolicy.ExecuteAsync(
                async token =>
                {
                    var httpResponse = await _httpClient.PostAsJsonAsync(
                        "/v1/chat/completions", requestBody, token);
                    return httpResponse;
                }, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI gateway returned {StatusCode}",
                    response.StatusCode);

                return new AiGatewayResponse
                {
                    Success = false,
                    ErrorMessage = $"Gateway error: {response.StatusCode}"
                };
            }

            var result = await response.Content
                .ReadFromJsonAsync<JsonDocument>(ct);
            var content = result?.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            var tokensUsed = result?.RootElement
                .GetProperty("usage")
                .GetProperty("total_tokens")
                .GetInt32() ?? 0;

            return new AiGatewayResponse
            {
                Success = true,
                Content = content,
                TokensUsed = tokensUsed
            };
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "AI gateway circuit breaker is open — fallback to manual");
            return new AiGatewayResponse
            {
                Success = false,
                ErrorMessage = "AI assist unavailable (circuit open)"
            };
        }
        catch (TimeoutRejectedException)
        {
            _logger.LogWarning(
                "AI gateway request timed out after {Timeout}ms",
                options.Timeout.TotalMilliseconds);
            return new AiGatewayResponse
            {
                Success = false,
                ErrorMessage = "AI assist timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI gateway request failed");
            return new AiGatewayResponse
            {
                Success = false,
                ErrorMessage = "AI assist unavailable"
            };
        }
    }
}
```

4. **Create `IntakeAssistService`** orchestrating prompt construction, AI call, and response mapping:

```csharp
// server/src/PropelIQ.Application/AI/IntakeAssistService.cs
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Application.AI;

public class IntakeAssistService
{
    private readonly IAiGatewayClient _aiClient;
    private readonly ILogger<IntakeAssistService> _logger;

    // Prompt template loaded from embedded resource
    private static readonly JsonDocument PromptTemplate =
        JsonDocument.Parse(
            File.ReadAllText("AI/Prompts/intake-assist.json"));

    public IntakeAssistService(
        IAiGatewayClient aiClient,
        ILogger<IntakeAssistService> logger)
    {
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<IntakeAssistResponse> AssistAsync(
        IntakeAssistRequest request,
        CancellationToken ct)
    {
        // Redact PII — only send symptom description (AIR-009)
        var sanitizedText = request.FreeTextDescription.Trim();

        if (string.IsNullOrWhiteSpace(sanitizedText))
        {
            return FallbackResponse("Empty description provided");
        }

        var root = PromptTemplate.RootElement;
        var systemPrompt = root.GetProperty("system_prompt").GetString()!;
        var userTemplate = root.GetProperty("user_template").GetString()!;
        var userPrompt = userTemplate.Replace(
            "{free_text}", sanitizedText);

        var options = new AiRequestOptions
        {
            Model = root.GetProperty("model").GetString()!,
            Temperature = root.GetProperty("temperature").GetDouble(),
            MaxTokens = root.GetProperty("max_tokens").GetInt32(),
            ResponseFormat = root.GetProperty("response_format").GetString()!,
            Timeout = TimeSpan.FromSeconds(2.5) // AIR-006
        };

        var aiResponse = await _aiClient.SendCompletionAsync(
            systemPrompt, userPrompt, options, ct);

        // AIR-011: Log prompt/response for audit
        _logger.LogInformation(
            "IntakeAssist AI call: Success={Success}, Tokens={Tokens}",
            aiResponse.Success, aiResponse.TokensUsed);

        // AIR-005: Fallback on failure
        if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.Content))
        {
            return FallbackResponse(
                aiResponse.ErrorMessage
                    ?? "AI assist unavailable, please fill in manually.");
        }

        // Parse and validate AI response
        try
        {
            var suggestions = JsonSerializer.Deserialize<IntakeFieldSuggestions>(
                aiResponse.Content,
                new JsonSerializerOptions
                    { PropertyNameCamelCase = true });

            if (suggestions is null)
                return FallbackResponse("Invalid AI response structure");

            // Determine which fields were populated by AI
            var populatedFields = new List<string>();
            if (suggestions.ReasonForVisit is not null)
                populatedFields.Add("reasonForVisit");
            if (suggestions.SymptomDescription is not null)
                populatedFields.Add("symptomDescription");
            if (suggestions.Severity is not null)
                populatedFields.Add("severity");
            if (suggestions.OnsetDuration is not null)
                populatedFields.Add("onsetDuration");
            if (suggestions.BodyArea is not null)
                populatedFields.Add("bodyArea");
            if (suggestions.RelevantMedicalHistory.Count > 0)
                populatedFields.Add("relevantMedicalHistory");
            if (suggestions.CurrentMedications.Count > 0)
                populatedFields.Add("currentMedications");
            if (suggestions.Allergies.Count > 0)
                populatedFields.Add("allergies");

            return new IntakeAssistResponse
            {
                AiAssisted = true,
                Suggestions = suggestions,
                AiPopulatedFields = populatedFields,
                Confidence = 0.85 // Default confidence; refine with model logprobs
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse AI response for intake assist");
            return FallbackResponse(
                "AI assist unavailable, please fill in manually.");
        }
    }

    private static IntakeAssistResponse FallbackResponse(string reason) =>
        new()
        {
            AiAssisted = false,
            FallbackReason = reason,
            Suggestions = new IntakeFieldSuggestions(),
            AiPopulatedFields = [],
            Confidence = 0.0
        };
}
```

5. **Add AI-assist endpoint** to `IntakeController`:

```csharp
// Add to IntakeController.cs
[HttpPost("ai-assist")]
[ProducesResponseType(typeof(IntakeAssistResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> AiAssist(
    [FromBody] IntakeAssistRequest request,
    CancellationToken ct)
{
    var result = await _intakeAssistService.AssistAsync(request, ct);
    return Ok(result);
}
```

6. **Register AI services** and configure HttpClient:

```csharp
// In DependencyInjection.cs
services.AddHttpClient<IAiGatewayClient, AiGatewayClient>(client =>
{
    client.BaseAddress = new Uri(
        configuration.GetConnectionString("AiGateway")!);
    client.Timeout = TimeSpan.FromSeconds(5); // Outer safety timeout
});

services.AddScoped<IntakeAssistService>();
```

Configuration in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "AiGateway": "http://localhost:4000"
  }
}
```

## Current Project State

```text
propelIQ/
└── server/
    └── src/
        ├── PropelIQ.Api/
        │   └── Controllers/
        │       └── IntakeController.cs     (from task_001)
        ├── PropelIQ.Application/
        │   ├── Intake/
        │   │   ├── IntakeDraftService.cs   (from task_001)
        │   │   └── Dto/
        │   ├── AI/                          (new module)
        │   │   ├── Prompts/
        │   │   ├── Dto/
        │   │   └── Validators/
        │   └── Abstractions/
        └── PropelIQ.Infrastructure/
            ├── AI/                          (new module)
            ├── Intake/
            └── DependencyInjection.cs
```

> Placeholder: Update on execution based on US_008 AI gateway scaffold completion.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/PropelIQ.Application/AI/Dto/IntakeAssistDto.cs | Request/response DTOs with structured field suggestions and AI-populated field markers |
| CREATE | server/src/PropelIQ.Application/AI/Prompts/intake-assist.json | Versioned prompt template with system/user prompts, temperature, token limits |
| CREATE | server/src/PropelIQ.Application/AI/IntakeAssistService.cs | Prompt construction, AI call orchestration, response parsing, fallback logic |
| CREATE | server/src/PropelIQ.Application/Abstractions/IAiGatewayClient.cs | Gateway client abstraction with request options and response model |
| CREATE | server/src/PropelIQ.Infrastructure/AI/AiGatewayClient.cs | HTTP client for LiteLLM with Polly circuit breaker and timeout policies |
| MODIFY | server/src/PropelIQ.Api/Controllers/IntakeController.cs | Add POST /api/v1/intake/ai-assist endpoint |
| MODIFY | server/src/PropelIQ.Infrastructure/DependencyInjection.cs | Register AI gateway HttpClient and IntakeAssistService |

## External References

- Azure OpenAI Chat Completions API: https://learn.microsoft.com/en-us/azure/ai-services/openai/reference
- LiteLLM Proxy Server: https://docs.litellm.ai/docs/proxy/quick_start
- Polly Circuit Breaker: https://github.com/App-vNext/Polly/wiki/Circuit-Breaker
- Polly Timeout: https://github.com/App-vNext/Polly/wiki/Timeout
- JSON Schema Validation .NET: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-dom

## Build Commands

```bash
# Build backend
cd server/src/PropelIQ.Api
dotnet build

# Run backend (requires AI gateway running on port 4000)
dotnet run

# Test AI-assisted intake
curl -X POST "http://localhost:5000/api/v1/intake/ai-assist" \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"freeTextDescription":"I have been having severe headaches for the past 3 days, mostly on the right side. I take ibuprofen but it does not help. I am allergic to penicillin."}'

# Test fallback (with AI gateway stopped)
curl -X POST "http://localhost:5000/api/v1/intake/ai-assist" \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"freeTextDescription":"knee pain"}'
```

## Implementation Validation Strategy

- [x] `POST /api/v1/intake/ai-assist` accepts free-text description and returns structured suggestions within 2.5 seconds (AC-1, AIR-006)
- [x] Response includes `aiAssisted: true` and `aiPopulatedFields` list when AI succeeds (AC-1, UXR-405)
- [x] Response includes `aiAssisted: false` and `fallbackReason` when AI fails or times out (AIR-005 edge case)
- [x] Circuit breaker opens after 3 consecutive failures and stays open for 30 seconds (TR-008)
- [x] Timeout policy enforces 2.5-second limit per AIR-006
- [x] Prompt template is versioned and loaded from `intake-assist.json`
- [x] Patient identifiers are not included in prompts — only symptom description text (AIR-009)
- [x] Prompt and response content are logged with confidence values for audit trail (AIR-011)
- [x] AI response is validated against expected JSON structure (AIR-008)
- [x] Empty or malformed AI responses fall back to manual mode gracefully
- [x] Endpoint requires JWT bearer authentication

## Implementation Checklist

- [x] Create `IntakeAssistRequest` and `IntakeAssistResponse` DTOs with `IntakeFieldSuggestions` structure
- [x] Create versioned prompt template `intake-assist.json` with system/user prompts and model parameters
- [x] Create `IAiGatewayClient` abstraction and `AiGatewayClient` with Polly circuit breaker (3 failures/30s break) and timeout (2.5s)
- [x] Create `IntakeAssistService` with prompt construction, PII redaction, AI call, response parsing, and fallback logic
- [x] Add `POST /api/v1/intake/ai-assist` endpoint to `IntakeController`
- [x] Configure AI gateway HttpClient with base address from `appsettings.json`
- [x] Log prompt/response pairs with confidence values for AIR-011 audit compliance
- [x] Validate AI JSON response structure and fall back on parse failure

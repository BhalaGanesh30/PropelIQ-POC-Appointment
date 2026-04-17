---
task_id: task_002
user_story: us_053
epic: EP-008
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_053] AI Gateway Integration with Circuit Breaker Fallback
- **Story Location**: [.propel/context/tasks/EP-008/us_053/us_053.md](.propel/context/tasks/EP-008/us_053/us_053.md)
- **Acceptance Criteria**:
  - AC-1: Given the AI gateway is healthy, When a coding suggestion request is sent, Then the request is routed to the configured AI provider and a response is returned within 2.5 seconds p95 (AIR-006).
  - AC-2: Given the AI provider returns errors for 5 consecutive requests within 60 seconds, When the circuit breaker threshold is reached, Then the circuit opens and all subsequent AI requests immediately return a fallback response prompting manual workflow.
  - AC-3: Given the circuit breaker is open, When a half-open probe succeeds after the recovery window, Then the circuit closes and AI-assisted requests resume normally.
  - AC-4: Given an AI request exceeds the configured timeout, When the timeout fires, Then the gateway cancels the request, logs the latency breach, and returns the manual coding fallback response.
- **Edge Cases**:
  - Edge Case 1: Rapid circuit cycling (circuit trips ≥ 3 times within 1 hour) — alert emitted via OpenTelemetry `ai.circuit_trip_count` counter; minimum hold-open time prevents immediate re-close.
  - Edge Case 2: Circuit open notification — all AI endpoint responses include `aiFallbackActive: bool` in response envelope; `GET /api/v1/ai-gateway/status` endpoint returns current circuit state for FE polling.

---

## Design References (Backend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A (backend task) |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | ASP.NET Core Web API | 8.x |
| ORM | N/A | N/A |
| Database | N/A | N/A |
| Cache | Redis (StackExchange.Redis) | 2.x |
| Observability | OpenTelemetry | latest |
| Frontend | N/A | N/A |
| AI/ML | Azure OpenAI GPT-4.1 via LiteLLM gateway | 2026 APIs |
| AI Gateway | LiteLLM + Polly (Microsoft.Extensions.Http.Resilience) | latest stable |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-005, AIR-006, AIR-011 |
| **AI Pattern** | Gateway with circuit-breaker fallback to deterministic manual flow |
| **Prompt Template Path** | N/A — this task implements the gateway infrastructure, not prompt content |
| **Guardrails Config** | Circuit-breaker thresholds via `IConfiguration["AI:CircuitBreaker:*"]`; timeout via `IConfiguration["AI:RequestTimeoutMs"]` |
| **Model Provider** | Azure OpenAI GPT-4.1 via LiteLLM gateway |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the AI Gateway infrastructure layer in `SharedServices` module (provider-agnostic per TR-008). This is the central resilience layer that all AI-calling services (`CodingAiGatewayClient` from US_049, `ExtractionWorkerService` from US_044) route through.

The gateway wraps the LiteLLM HTTP client with a **Polly v8** (`Microsoft.Extensions.Http.Resilience`) pipeline:

1. **Timeout policy**: `IConfiguration["AI:RequestTimeoutMs"]` (default 3000ms); on timeout → cancels request → logs `ai.timeout` event via OpenTelemetry → returns `AiGatewayFallbackResult` (AC-4).
2. **Retry policy**: 2 retries with exponential backoff (500ms, 1000ms) on transient HTTP errors (5xx, network failure). Retries do NOT count toward circuit breaker failure count.
3. **Circuit-breaker policy**: Opens after `IConfiguration["AI:CircuitBreaker:FailureThreshold"]` consecutive failures (default 5) within `IConfiguration["AI:CircuitBreaker:SamplingDurationSeconds"]` (default 60s). Hold-open time: `IConfiguration["AI:CircuitBreaker:BreakDurationSeconds"]` (default 30s). Half-open probe: 1 trial request after break duration (AC-3). On open → log `ai.circuit_opened` event; increment `ai.circuit_trip_count` counter (Edge Case 1 — alert after 3 trips/hour).
4. **Fallback**: When circuit is open or timeout fires, return `AiGatewayFallbackResult { FallbackActive = true, Reason }`. All AI callers check `result.FallbackActive` and return their manual-workflow response.

**Circuit state persistence**: Store current circuit state (`open/closed/half-open`, `lastTripAt`) in Redis with no TTL (state persisted until explicit reset). This enables `GET /api/v1/ai-gateway/status` to return current state without querying Polly internals.

**Response envelope**: A middleware `AiFallbackEnvelopeMiddleware` intercepts all `/api/v1/` responses and appends `"aiFallbackActive": true` to the response JSONB when `IAiGatewayStateService.IsCircuitOpen()` returns true (Edge Case 2).

No new DB tables required — state in Redis; events in OpenTelemetry.

---

## Dependent Tasks

- No upstream dependencies for this task — the gateway infrastructure is self-contained; US_049/US_050 `CodingAiGatewayClient` already calls LiteLLM directly (scaffolded). This task formalizes the shared gateway with Polly policy as a registered `IHttpClientFactory`-backed service that replaces the inline HTTP calls.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `IAiGatewayClient` | CREATE | Shared interface: `SendAsync(AiGatewayRequest req, CancellationToken ct): Task<AiGatewayResult>` |
| `AiGatewayClient` | CREATE | LiteLLM HTTP client wrapped with Polly resilience pipeline (timeout → retry → circuit-breaker) |
| `AiGatewayRequest` | CREATE | Shared request DTO: `string Prompt`, `string ModelId`, `JsonSchema? OutputSchema`, `int MaxTokens` |
| `AiGatewayResult` | CREATE | `string? Content`, `bool FallbackActive`, `string? FallbackReason`, `decimal? Confidence` |
| `IAiGatewayStateService` | CREATE | Interface: `IsCircuitOpen(): bool`; `GetStatus(): AiGatewayStatusDto`; `RecordTrip()` |
| `AiGatewayStateService` | CREATE | Redis-backed state: `circuit_state`, `last_trip_at`, `trip_count_hour`; `RecordTrip` increments hourly counter and alerts if ≥ 3 (Edge Case 1) |
| `AiFallbackEnvelopeMiddleware` | CREATE | ASP.NET Core middleware: appends `aiFallbackActive` to all API responses when circuit open (Edge Case 2) |
| `AiGatewayController` | CREATE | `GET /api/v1/ai-gateway/status` — returns `AiGatewayStatusDto` for FE polling; `[Authorize(Roles = "Clinician,Staff")]` |
| `AiGatewayStatusDto` | CREATE | `{ CircuitState: 'closed'|'open'|'half-open', FallbackActive: bool, LastTripAt: DateTimeOffset? }` |
| `CodingAiGatewayClient` | MODIFY | Replace inline `HttpClient` calls with injected `IAiGatewayClient`; delegate to shared gateway |
| `AiGatewayClient` DI registration | CREATE | Register via `AddHttpClient<AiGatewayClient>().AddResilienceHandler("ai-pipeline", builder => { ... })` |
| `SharedServicesModule` DI | MODIFY | Register `IAiGatewayClient`, `IAiGatewayStateService`, `AiFallbackEnvelopeMiddleware` |
| `Program.cs` | MODIFY | Add `app.UseMiddleware<AiFallbackEnvelopeMiddleware>()` |

---

## Implementation Plan

1. **Create shared DTOs**: `AiGatewayRequest` (`string Prompt`, `string ModelId`, `JsonSchema? OutputSchema`, `int MaxTokens`); `AiGatewayResult` (`string? Content`, `bool FallbackActive`, `string? FallbackReason`); `AiGatewayStatusDto` (`string CircuitState`, `bool FallbackActive`, `DateTimeOffset? LastTripAt`).
2. **Create `IAiGatewayStateService` / `AiGatewayStateService`**: Redis keys: `ai:circuit:state` (string: `closed`/`open`/`half-open`), `ai:circuit:last_trip_at` (TIMESTAMPTZ string), `ai:circuit:trip_count:{hour}` (integer with 1-hour expiry). `IsCircuitOpen()` reads `ai:circuit:state`. `RecordTrip()` sets state to `open`, sets `last_trip_at`, increments hourly counter; if `trip_count > 3` emits `ai.circuit_rapid_cycling` OpenTelemetry event for operations alerting (Edge Case 1).
3. **Create `IAiGatewayClient` / `AiGatewayClient`**: Register via `IHttpClientFactory` + `AddResilienceHandler("ai-pipeline")`. Polly pipeline configuration (in order): **Timeout**: `TimeoutStrategyOptions { Timeout = TimeSpan.FromMilliseconds(config["AI:RequestTimeoutMs"]) }` — on timeout, log `ai.timeout` span event, call `stateService.SetState("open")`, return `AiGatewayResult { FallbackActive = true, FallbackReason = "Timeout" }` (AC-4). **Retry**: `RetryStrategyOptions { MaxRetryAttempts = 2, BackoffType = DelayBackoffType.Exponential, Delay = 500ms }` on `HttpRequestException` / 5xx. **Circuit-breaker**: `CircuitBreakerStrategyOptions { FailureRatio = configThreshold, SamplingDuration = configSeconds, BreakDuration = configBreakSeconds, MinimumThroughput = 5 }`. On `OnOpened` callback: call `stateService.RecordTrip()`; emit `ai.circuit_opened` span. On `OnClosed` callback: call `stateService.SetState("closed")`; emit `ai.circuit_closed` span. On `OnHalfOpened` callback: call `stateService.SetState("half-open")` (AC-3).
4. **Implement `AiGatewayClient.SendAsync`**: When circuit open → return fallback immediately (Polly `BrokenCircuitException` caught → `FallbackActive = true`). When healthy → `POST {litellmBaseUrl}/chat/completions` with `AiGatewayRequest` mapped to OpenAI chat format; deserialize response; return `AiGatewayResult { Content = response.choices[0].message.content, FallbackActive = false }` (AC-1).
5. **Create `AiFallbackEnvelopeMiddleware`**: After response body is written (`HttpContext.Response.OnStarting`), check `IAiGatewayStateService.IsCircuitOpen()`; if true and response Content-Type is `application/json`, inject `"aiFallbackActive": true` into JSON response body using `System.Text.Json` (Edge Case 2, AC-2). Apply only to paths matching `/api/v1/*`.
6. **Create `AiGatewayController`**: `[HttpGet("ai-gateway/status")]`; `[Authorize(Roles = "Clinician,Staff")]`; calls `stateService.GetStatus()`; returns `AiGatewayStatusDto` (Edge Case 2).
7. **Modify `CodingAiGatewayClient`** (US_049): Replace inline `HttpClient` with injected `IAiGatewayClient`; check `result.FallbackActive` on every call and propagate as `lowConfidence: true` + manual fallback flag upstream. Same modification pattern applies to `ExtractionWorkerService` (US_044).
8. **OpenTelemetry instrumentation**: Metrics: `ai.circuit_trip_count` counter; `ai.request_duration_ms` histogram with `circuit_state` tag; `ai.timeout_count` counter; `ai.circuit_rapid_cycling` counter (Edge Case 1 — operations alert). Register `MeterProvider` in `SharedServicesModule`.

---

## Current Project State

```
src/
├── Modules/
│   ├── SharedServices/
│   │   ├── AI/
│   │   │   ├── IAiGatewayClient.cs                   ← CREATE
│   │   │   ├── AiGatewayClient.cs                    ← CREATE (Polly pipeline)
│   │   │   ├── IAiGatewayStateService.cs             ← CREATE
│   │   │   ├── AiGatewayStateService.cs              ← CREATE (Redis-backed)
│   │   │   ├── AiGatewayRequest.cs                   ← CREATE
│   │   │   ├── AiGatewayResult.cs                    ← CREATE
│   │   │   └── AiGatewayStatusDto.cs                 ← CREATE
│   │   ├── Middleware/
│   │   │   └── AiFallbackEnvelopeMiddleware.cs       ← CREATE
│   │   └── [existing SharedServices structure...]
│   ├── ClinicalIntelligence/
│   │   ├── AI/
│   │   │   └── CodingAiGatewayClient.cs              ← MODIFY (inject IAiGatewayClient)
│   │   └── [existing structure...]
├── Api/
│   ├── Controllers/
│   │   └── AiGatewayController.cs                    ← CREATE
│   └── Program.cs                                    ← MODIFY (register middleware)
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Modules/SharedServices/AI/IAiGatewayClient.cs` | Shared gateway interface: SendAsync |
| CREATE | `Modules/SharedServices/AI/AiGatewayClient.cs` | Polly pipeline: timeout → retry → circuit-breaker; LiteLLM HTTP call; fallback on open/timeout |
| CREATE | `Modules/SharedServices/AI/IAiGatewayStateService.cs` | Circuit state interface |
| CREATE | `Modules/SharedServices/AI/AiGatewayStateService.cs` | Redis-backed state; RecordTrip with hourly counter; rapid cycling alert (Edge Case 1) |
| CREATE | `Modules/SharedServices/AI/AiGatewayRequest.cs` | Request DTO: Prompt, ModelId, OutputSchema, MaxTokens |
| CREATE | `Modules/SharedServices/AI/AiGatewayResult.cs` | Result DTO: Content, FallbackActive, FallbackReason |
| CREATE | `Modules/SharedServices/AI/AiGatewayStatusDto.cs` | Status DTO: CircuitState, FallbackActive, LastTripAt |
| CREATE | `Modules/SharedServices/Middleware/AiFallbackEnvelopeMiddleware.cs` | Appends aiFallbackActive to JSON responses when circuit open (Edge Case 2) |
| CREATE | `Api/Controllers/AiGatewayController.cs` | GET /api/v1/ai-gateway/status; Clinician+Staff |
| MODIFY | `Modules/ClinicalIntelligence/AI/CodingAiGatewayClient.cs` | Replace inline HttpClient with IAiGatewayClient; check FallbackActive |
| MODIFY | `Api/Program.cs` | Register AiFallbackEnvelopeMiddleware + Polly resilience handler DI |

---

## External References

- Microsoft.Extensions.Http.Resilience (Polly v8): https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
- Polly circuit breaker: https://www.pollydocs.org/strategies/circuit-breaker.html
- StackExchange.Redis: https://stackexchange.github.io/StackExchange.Redis/
- OpenTelemetry .NET metrics: https://opentelemetry.io/docs/languages/dotnet/instrumentation/#creating-metrics
- TR-008: AI orchestration through provider-agnostic gateway with circuit-breaker fallback
- AIR-005: Fallback to deterministic manual workflows when model confidence below threshold or AI unavailable
- AIR-006: AI response latency within 2.5 seconds p95 — timeout policy enforces this (AC-1, AC-4)
- AIR-011: Log prompts, context references, model responses — `ai.request_duration_ms` histogram + span events

---

## Build Commands

```bash
# Restore packages
dotnet restore

# Build solution
dotnet build --no-restore

# Run unit tests
dotnet test --no-build --filter "Category=Unit"

# Run integration tests
dotnet test --no-build --filter "Category=Integration"

# Run the API
dotnet run --project src/Api/Api.csproj
```

---

## Implementation Validation Strategy

- [ ] Healthy LiteLLM call returns `FallbackActive = false` within 2.5s p95 (AC-1, AIR-006)
- [ ] After 5 consecutive 5xx errors in 60s — `AiGatewayStateService` state = `open`; subsequent calls return `FallbackActive = true` immediately without calling LiteLLM (AC-2)
- [ ] After break duration — half-open probe sent; on probe success → state = `closed`, AI resumes (AC-3)
- [ ] Request exceeding configured timeout — cancelled; `ai.timeout` span event logged; `FallbackActive = true` returned (AC-4)
- [ ] 3 circuit trips within 1 hour → `ai.circuit_rapid_cycling` counter incremented (Edge Case 1)
- [ ] All `/api/v1/` JSON responses contain `"aiFallbackActive": true` while circuit open (Edge Case 2)
- [ ] `GET /api/v1/ai-gateway/status` returns `{ circuitState: 'open', fallbackActive: true }` while open (Edge Case 2)
- [ ] `CodingAiGatewayClient` propagates `FallbackActive = true` as manual fallback to caller; no LiteLLM call made while circuit open

---

## Implementation Checklist

- [ ] Create `AiGatewayRequest`, `AiGatewayResult`, `AiGatewayStatusDto` shared DTOs
- [ ] Create `IAiGatewayStateService` / `AiGatewayStateService`: Redis state; `RecordTrip` with hourly counter; `ai.circuit_rapid_cycling` alert (Edge Case 1)
- [ ] Create `IAiGatewayClient` / `AiGatewayClient`: Polly timeout → retry → circuit-breaker pipeline; `OnOpened`/`OnClosed`/`OnHalfOpened` callbacks update Redis state; fallback on open or timeout (AC-2, AC-3, AC-4)
- [ ] Create `AiFallbackEnvelopeMiddleware`: appends `aiFallbackActive` to JSON responses when circuit open (Edge Case 2)
- [ ] Create `AiGatewayController`: GET /api/v1/ai-gateway/status (Clinician+Staff) (Edge Case 2)
- [ ] Modify `CodingAiGatewayClient` (US_049): inject `IAiGatewayClient`; check `FallbackActive` flag
- [ ] Register middleware + Polly `AddResilienceHandler` in Program.cs; register `IAiGatewayClient`, `IAiGatewayStateService` in SharedServices DI
- [ ] Add OpenTelemetry metrics: `ai.circuit_trip_count`, `ai.request_duration_ms`, `ai.timeout_count`, `ai.circuit_rapid_cycling` (AIR-011)

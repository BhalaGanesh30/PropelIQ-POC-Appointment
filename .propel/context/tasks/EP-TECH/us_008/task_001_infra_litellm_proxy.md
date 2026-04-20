# Task - TASK_001

## Requirement Reference

- User Story: us_008
- Story Location: .propel/context/tasks/EP-TECH/us_008/us_008.md
- Acceptance Criteria:
  - AC-1: Given the AI gateway is configured, When a coding suggestion request is sent through the gateway, Then the request is routed to the configured model provider and a structured response is returned.
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
| Backend | N/A | N/A |
| Database | N/A | N/A |
| Library | LiteLLM Proxy | latest stable |
| Library | Docker Compose | latest stable |
| AI/ML | Azure OpenAI GPT-4.1 family | 2026 APIs |
| Vector Store | N/A | N/A |
| AI Gateway | LiteLLM-compatible gateway | latest stable |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-005, AIR-006 |
| **AI Pattern** | Hybrid |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | Azure OpenAI |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Provision the LiteLLM proxy server as a Docker Compose service that acts as the centralized AI gateway for the PropelIQ platform. The proxy exposes an OpenAI-compatible API (`/chat/completions`, `/embeddings`) and routes requests to the configured Azure OpenAI model provider. The `config.yaml` defines model routing with named model aliases, retry policies with exponential backoff (`num_retries: 3`), fallback chains between model deployments, and circuit breaker settings (`allowed_fails: 3`, `cooldown_time: 30`). API key authentication is enforced via LiteLLM's built-in key management. The configuration includes a startup validation step so a malformed `config.yaml` causes the container to fail with a descriptive error and prevents the application stack from proceeding.

## Dependent Tasks

- US_005 tasks (requires base docker-compose.yml)

## Impacted Components

- Modify: `docker-compose.yml` (add litellm-gateway service)
- New: `infra/litellm/config.yaml` (LiteLLM proxy configuration with model list, retry, fallback, circuit breaker)
- Modify: `.env.example` (add AZURE_OPENAI_API_KEY, AZURE_OPENAI_ENDPOINT, LITELLM_MASTER_KEY variables)

## Implementation Plan

1. **Add LiteLLM proxy service** to `docker-compose.yml`. The service runs the official `ghcr.io/berriai/litellm` image with the config file mounted and health checks enabled:

```yaml
litellm-gateway:
  image: ghcr.io/berriai/litellm:v1.81.9-stable
  container_name: propeliq-litellm
  command: ["--config", "/app/config.yaml", "--detailed_debug"]
  volumes:
    - ./infra/litellm/config.yaml:/app/config.yaml:ro
  ports:
    - "4000:4000"
  environment:
    - AZURE_OPENAI_API_KEY=${AZURE_OPENAI_API_KEY}
    - AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
    - LITELLM_MASTER_KEY=${LITELLM_MASTER_KEY}
  healthcheck:
    test: ["CMD", "wget", "--spider", "-q", "http://localhost:4000/health"]
    interval: 15s
    timeout: 5s
    retries: 5
    start_period: 10s
  restart: unless-stopped
```

The `--detailed_debug` flag ensures malformed config produces verbose error output (edge case: descriptive validation error). If `config.yaml` is syntactically invalid or contains missing required fields, the LiteLLM process exits with a non-zero code and Docker marks the container as unhealthy.

2. **Create `infra/litellm/config.yaml`** with model list, retry, fallback, and circuit breaker configuration aligned to design.md Decision 6 and TR-008:

```yaml
model_list:
  # Primary: Azure OpenAI GPT-4.1 deployment
  - model_name: gpt-4.1
    litellm_params:
      model: azure/gpt-4.1
      api_key: os.environ/AZURE_OPENAI_API_KEY
      api_base: os.environ/AZURE_OPENAI_ENDPOINT
      api_version: "2026-01-01-preview"
      timeout: 10
      max_retries: 0  # Retries handled at router level
    tpm: 60000
    rpm: 600

  # Fallback: Azure OpenAI GPT-4.1-mini (lower cost/latency)
  - model_name: gpt-4.1-mini
    litellm_params:
      model: azure/gpt-4.1-mini
      api_key: os.environ/AZURE_OPENAI_API_KEY
      api_base: os.environ/AZURE_OPENAI_ENDPOINT
      api_version: "2026-01-01-preview"
      timeout: 10
      max_retries: 0
    tpm: 120000
    rpm: 1200

  # Alias for coding suggestions (maps to primary)
  - model_name: coding-suggestion
    litellm_params:
      model: azure/gpt-4.1
      api_key: os.environ/AZURE_OPENAI_API_KEY
      api_base: os.environ/AZURE_OPENAI_ENDPOINT
      api_version: "2026-01-01-preview"
      timeout: 10

litellm_settings:
  # Retry with exponential backoff (AC-4)
  num_retries: 3
  request_timeout: 10

  # Fallback chain: primary -> mini (AC-4)
  fallbacks:
    - gpt-4.1: ["gpt-4.1-mini"]
    - coding-suggestion: ["gpt-4.1-mini"]

  # Circuit breaker settings (AC-2, AC-4)
  allowed_fails: 3        # Failures before cooldown
  cooldown_time: 30       # Seconds to cool down a failed deployment

  # Token tracking
  success_callback: []
  failure_callback: []

  # Drop unmapped params to prevent leaking to providers
  drop_params: true

general_settings:
  # Master key authentication (edge case: API key validation)
  master_key: os.environ/LITELLM_MASTER_KEY
```

3. **Configure API key authentication** via LiteLLM's master key mechanism. When `master_key` is set in `general_settings`, all requests to the proxy must include `Authorization: Bearer <LITELLM_MASTER_KEY>`. Requests without a valid key receive HTTP 401 and no model call is made (edge case). The master key is sourced from environment variable to avoid secrets in config files.

4. **Add environment variables** to `.env.example`:

```bash
# AI Gateway (LiteLLM)
AZURE_OPENAI_API_KEY=your-azure-openai-api-key
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
LITELLM_MASTER_KEY=sk-propeliq-gateway-key
```

5. **Configure Docker Compose dependency** so the API service depends on the gateway being healthy:

```yaml
api:
  depends_on:
    litellm-gateway:
      condition: service_healthy
```

6. **Validate startup behavior** for malformed config (edge case). LiteLLM proxy performs YAML parsing and model list validation on startup. A malformed file (syntax error, missing required fields, invalid model references) causes the process to exit with error output describing the issue. Docker Compose `restart: unless-stopped` will not mask the error since consecutive failures trigger container stop.

## Current Project State

```text
propelIQ/
├── docker-compose.yml       (from US_005)
├── .env.example
├── server/
│   └── src/
│       └── PropelIQ.Api/
├── infra/
│   ├── otel-collector/      (from US_007)
│   ├── prometheus/           (from US_007)
│   ├── grafana/              (from US_007)
│   └── loki/                 (from US_007)
└── README.md
```

> Placeholder: Update on execution based on US_005 and US_007 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | docker-compose.yml | Add litellm-gateway service with health check and environment variables |
| CREATE | infra/litellm/config.yaml | LiteLLM proxy config with model list, retry/fallback/circuit breaker settings |
| MODIFY | .env.example | Add AZURE_OPENAI_API_KEY, AZURE_OPENAI_ENDPOINT, LITELLM_MASTER_KEY |

## External References

- LiteLLM Proxy configuration: https://docs.litellm.ai/docs/proxy/configs
- LiteLLM reliability (retries, fallbacks): https://docs.litellm.ai/docs/proxy/reliability
- LiteLLM routing and circuit breaker: https://docs.litellm.ai/docs/routing
- LiteLLM Docker deployment: https://docs.litellm.ai/docs/proxy/deploy
- LiteLLM API key management: https://docs.litellm.ai/docs/proxy/virtual_keys
- Azure OpenAI provider configuration: https://docs.litellm.ai/docs/providers/azure
- LiteLLM health check endpoint: https://docs.litellm.ai/docs/proxy/health
- Docker Compose depends_on: https://docs.docker.com/compose/compose-file/05-services/#depends_on

## Build Commands

```bash
# Start LiteLLM gateway
docker compose up -d litellm-gateway

# Verify gateway health
curl http://localhost:4000/health

# Test model routing (requires valid API key)
curl -X POST http://localhost:4000/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${LITELLM_MASTER_KEY}" \
  -d '{"model": "coding-suggestion", "messages": [{"role": "user", "content": "test"}]}'

# Test 401 rejection (no key)
curl -X POST http://localhost:4000/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model": "coding-suggestion", "messages": [{"role": "user", "content": "test"}]}'

# View logs for troubleshooting
docker compose logs litellm-gateway --tail 50
```

## Implementation Validation Strategy

- [ ] LiteLLM proxy container starts healthy and responds to `/health` endpoint
- [ ] Request to `/chat/completions` with model `coding-suggestion` routes to Azure OpenAI and returns structured response (AC-1)
- [ ] Retry with exponential backoff executes up to 3 times before fallback activates (AC-4)
- [ ] Fallback chain routes to `gpt-4.1-mini` when primary `gpt-4.1` is unavailable (AC-4)
- [ ] Circuit breaker cools down deployment after 3 failures within cooldown window (AC-4)
- [ ] Malformed `config.yaml` prevents container from starting with descriptive error in logs (edge case)
- [ ] Request without valid `Authorization: Bearer` header returns HTTP 401 (edge case)
- [ ] Environment variables for API keys are not hardcoded in config files

## Implementation Checklist

- [x] Add `litellm-gateway` service to `docker-compose.yml` with `ghcr.io/berriai/litellm:v1.81.9-stable` image, health check, and port 4000
- [x] Create `infra/litellm/config.yaml` with `model_list` defining `gpt-4.1`, `gpt-4.1-mini`, and `coding-suggestion` model aliases
- [x] Configure `litellm_settings` with `num_retries: 3`, `request_timeout: 10`, fallback chains, `allowed_fails: 3`, and `cooldown_time: 30`
- [x] Set `general_settings.master_key` from `LITELLM_MASTER_KEY` environment variable for API key authentication
- [x] Add `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_ENDPOINT`, and `LITELLM_MASTER_KEY` to `.env.example`
- [x] Add `depends_on` condition so the API service waits for `litellm-gateway` to be healthy
- [x] Verify malformed config startup failure produces descriptive error output
- [x] Verify HTTP 401 response for unauthenticated requests with no model call executed

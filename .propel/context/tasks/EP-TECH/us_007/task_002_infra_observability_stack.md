# Task - TASK_002

## Requirement Reference

- User Story: us_007
- Story Location: .propel/context/tasks/EP-TECH/us_007/us_007.md
- Acceptance Criteria:
  - AC-3: Given the observability stack (Prometheus, Grafana, Loki) is running via Compose, When metrics are scraped, Then request rate, error rate, and latency percentile metrics are visible in the Grafana dashboard.
- Edge Case:
  - What happens if the telemetry exporter is unreachable? Exporter falls back to console output; application continues without blocking request processing.

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
| Library | OpenTelemetry Collector | latest stable |
| Library | Prometheus | latest stable |
| Library | Grafana | latest stable |
| Library | Grafana Loki | latest stable |
| Library | Docker Compose | latest stable |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Provision the full observability stack (OpenTelemetry Collector, Prometheus, Grafana, Loki) via Docker Compose and configure it to ingest traces, metrics, and logs from the ASP.NET Core API. The OpenTelemetry Collector receives OTLP data from the API and routes traces to console/Jaeger (for dev), metrics to Prometheus, and logs to Loki. Prometheus scrapes both the Collector and the API's `/metrics` endpoint. Grafana is pre-provisioned with Prometheus and Loki data sources and a dashboard displaying request rate, error rate, and latency percentile (p50, p95, p99) panels. This delivers AC-3 end-to-end: metrics are visible in Grafana when the stack is running via Compose.

## Dependent Tasks

- task_001_be_opentelemetry_instrumentation (requires API emitting OTLP data and exposing /metrics)
- US_005 tasks (requires base docker-compose.yml)

## Impacted Components

- Modify: `docker-compose.yml` (add otel-collector, prometheus, grafana, loki services)
- New: `infra/otel-collector/otel-collector-config.yaml` (Collector pipeline configuration)
- New: `infra/prometheus/prometheus.yml` (Prometheus scrape configuration)
- New: `infra/grafana/provisioning/datasources/datasources.yml` (auto-provision Prometheus + Loki data sources)
- New: `infra/grafana/provisioning/dashboards/dashboard.yml` (dashboard provisioning config)
- New: `infra/grafana/provisioning/dashboards/propeliq-overview.json` (pre-built Grafana dashboard)
- New: `infra/loki/loki-config.yaml` (Loki local storage configuration)

## Implementation Plan

1. **Add OpenTelemetry Collector service** to `docker-compose.yml`. The Collector receives OTLP (gRPC on 4317, HTTP on 4318) from the API and exports to Prometheus and Loki:

```yaml
otel-collector:
  image: otel/opentelemetry-collector-contrib:latest
  container_name: propeliq-otel-collector
  command: ["--config", "/etc/otel-collector-config.yaml"]
  volumes:
    - ./infra/otel-collector/otel-collector-config.yaml:/etc/otel-collector-config.yaml:ro
  ports:
    - "4317:4317"   # OTLP gRPC
    - "4318:4318"   # OTLP HTTP
    - "8889:8889"   # Prometheus metrics from collector
  depends_on:
    loki:
      condition: service_started
  healthcheck:
    test: ["CMD", "wget", "--spider", "-q", "http://localhost:13133/"]
    interval: 10s
    timeout: 5s
    retries: 5
  restart: unless-stopped
```

2. **Create `infra/otel-collector/otel-collector-config.yaml`** with a pipeline that receives OTLP, batches telemetry, and exports to the appropriate backends:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 5s
    send_batch_size: 1024

exporters:
  prometheus:
    endpoint: "0.0.0.0:8889"
    namespace: propeliq
    resource_to_telemetry_conversion:
      enabled: true

  loki:
    endpoint: "http://loki:3100/loki/api/v1/push"
    default_labels_enabled:
      exporter: true
      job: true
      instance: true
      level: true

  debug:
    verbosity: basic

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [debug]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [prometheus]
    logs:
      receivers: [otlp]
      processors: [batch]
      exporters: [loki]
```

3. **Add Prometheus service** to `docker-compose.yml` with scrape configuration targeting both the API's `/metrics` Prometheus endpoint and the Collector's metrics endpoint:

```yaml
prometheus:
  image: prom/prometheus:latest
  container_name: propeliq-prometheus
  volumes:
    - ./infra/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
    - prometheus-data:/prometheus
  ports:
    - "9090:9090"
  depends_on:
    otel-collector:
      condition: service_healthy
  healthcheck:
    test: ["CMD", "wget", "--spider", "-q", "http://localhost:9090/-/healthy"]
    interval: 10s
    timeout: 5s
    retries: 5
  restart: unless-stopped
```

4. **Create `infra/prometheus/prometheus.yml`** with scrape targets:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: "propeliq-api"
    metrics_path: /metrics
    static_configs:
      - targets: ["api:5000"]
        labels:
          service: "propeliq-api"

  - job_name: "otel-collector"
    static_configs:
      - targets: ["otel-collector:8889"]
        labels:
          service: "otel-collector"
```

5. **Add Loki service** to `docker-compose.yml` for log aggregation:

```yaml
loki:
  image: grafana/loki:latest
  container_name: propeliq-loki
  volumes:
    - ./infra/loki/loki-config.yaml:/etc/loki/local-config.yaml:ro
    - loki-data:/loki
  ports:
    - "3100:3100"
  command: -config.file=/etc/loki/local-config.yaml
  healthcheck:
    test: ["CMD", "wget", "--spider", "-q", "http://localhost:3100/ready"]
    interval: 10s
    timeout: 5s
    retries: 5
  restart: unless-stopped
```

6. **Create `infra/loki/loki-config.yaml`** with local filesystem storage suitable for development:

```yaml
auth_enabled: false

server:
  http_listen_port: 3100

common:
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    kvstore:
      store: inmemory

schema_config:
  configs:
    - from: 2024-01-01
      store: tsdb
      object_store: filesystem
      schema: v13
      index:
        prefix: index_
        period: 24h

limits_config:
  allow_structured_metadata: true
  volume_enabled: true
```

7. **Add Grafana service** to `docker-compose.yml` with auto-provisioned data sources and dashboards:

```yaml
grafana:
  image: grafana/grafana:latest
  container_name: propeliq-grafana
  environment:
    - GF_SECURITY_ADMIN_USER=admin
    - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD:-admin}
    - GF_AUTH_ANONYMOUS_ENABLED=true
    - GF_AUTH_ANONYMOUS_ORG_ROLE=Viewer
  volumes:
    - ./infra/grafana/provisioning:/etc/grafana/provisioning:ro
    - grafana-data:/var/lib/grafana
  ports:
    - "3000:3000"
  depends_on:
    prometheus:
      condition: service_healthy
    loki:
      condition: service_healthy
  healthcheck:
    test: ["CMD", "wget", "--spider", "-q", "http://localhost:3000/api/health"]
    interval: 10s
    timeout: 5s
    retries: 5
  restart: unless-stopped
```

8. **Create Grafana provisioning files**:

**`infra/grafana/provisioning/datasources/datasources.yml`**:
```yaml
apiVersion: 1
datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: false

  - name: Loki
    type: loki
    access: proxy
    url: http://loki:3100
    editable: false
```

**`infra/grafana/provisioning/dashboards/dashboard.yml`**:
```yaml
apiVersion: 1
providers:
  - name: PropelIQ
    type: file
    disableDeletion: true
    editable: false
    options:
      path: /etc/grafana/provisioning/dashboards
      foldersFromFilesStructure: false
```

9. **Create `infra/grafana/provisioning/dashboards/propeliq-overview.json`** — a pre-built Grafana dashboard JSON with these panels satisfying AC-3:

| Panel | PromQL Query | Description |
|-------|-------------|-------------|
| Request Rate | `rate(propeliq_http_requests_total[5m])` | Requests per second |
| Error Rate | `rate(propeliq_errors_total[5m])` | Errors per second |
| Latency p50 | `histogram_quantile(0.5, rate(propeliq_http_duration_bucket[5m]))` | Median request latency |
| Latency p95 | `histogram_quantile(0.95, rate(propeliq_http_duration_bucket[5m]))` | 95th percentile latency |
| Latency p99 | `histogram_quantile(0.99, rate(propeliq_http_duration_bucket[5m]))` | 99th percentile latency |
| Logs Panel | Loki data source, `{service="propeliq-api"}` | Live log stream from API |

10. **Add named volumes** to `docker-compose.yml`:

```yaml
volumes:
  prometheus-data:
  grafana-data:
  loki-data:
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml       (from US_005 — PostgreSQL, Redis, API, Angular)
├── .env.example
├── server/
│   └── src/
│       └── PropelIQ.Api/    (with OTel instrumentation from task_001)
├── app/
├── infra/                   (to be created)
└── README.md
```

> Placeholder: Update on execution based on US_005 and task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | docker-compose.yml | Add otel-collector, prometheus, grafana, loki services and named volumes |
| CREATE | infra/otel-collector/otel-collector-config.yaml | OTLP receiver, batch processor, Prometheus + Loki + debug exporters |
| CREATE | infra/prometheus/prometheus.yml | Scrape config for API /metrics and otel-collector endpoints |
| CREATE | infra/loki/loki-config.yaml | Loki local filesystem storage config for development |
| CREATE | infra/grafana/provisioning/datasources/datasources.yml | Auto-provision Prometheus and Loki data sources |
| CREATE | infra/grafana/provisioning/dashboards/dashboard.yml | Dashboard provisioning file pointing to JSON dashboards |
| CREATE | infra/grafana/provisioning/dashboards/propeliq-overview.json | Pre-built dashboard with request rate, error rate, and latency panels |
| MODIFY | .env.example | Add GRAFANA_ADMIN_PASSWORD variable |

## External References

- OpenTelemetry Collector configuration: https://opentelemetry.io/docs/collector/configuration/
- OpenTelemetry Collector contrib (Loki exporter): https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/lokiexporter
- Prometheus configuration: https://prometheus.io/docs/prometheus/latest/configuration/configuration/
- Grafana provisioning: https://grafana.com/docs/grafana/latest/administration/provisioning/
- Grafana dashboard JSON model: https://grafana.com/docs/grafana/latest/dashboards/build-dashboards/view-dashboard-json-model/
- Loki configuration: https://grafana.com/docs/loki/latest/configure/
- Loki LogQL query language: https://grafana.com/docs/loki/latest/logql/
- PromQL query reference: https://prometheus.io/docs/prometheus/latest/querying/basics/
- Docker Compose healthchecks: https://docs.docker.com/compose/compose-file/05-services/#healthcheck

## Build Commands

```bash
# Start full observability stack
docker compose up -d otel-collector prometheus grafana loki

# Verify services are healthy
docker compose ps

# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Access Grafana dashboard
# Open http://localhost:3000 (admin/admin)

# Query Loki logs
curl -G http://localhost:3100/loki/api/v1/query --data-urlencode 'query={service="propeliq-api"}'

# Tear down
docker compose down -v
```

## Implementation Validation Strategy

- [ ] All observability services (otel-collector, prometheus, grafana, loki) start healthy via `docker compose up`
- [ ] Prometheus targets page (`http://localhost:9090/targets`) shows API and otel-collector as UP
- [ ] Grafana dashboard at `http://localhost:3000` displays request rate, error rate, and latency percentile panels (AC-3)
- [ ] Sending HTTP requests to the API produces visible metrics in the Grafana dashboard panels
- [ ] Loki receives structured logs from the API via otel-collector and they are queryable in Grafana Explore
- [ ] Application continues functioning when otel-collector is stopped (console fallback — edge case)
- [ ] Named volumes persist data across `docker compose down` and `docker compose up` cycles

## Implementation Checklist

- [x] Add otel-collector service to `docker-compose.yml` with OTLP ports (4317, 4318) and health check
- [x] Create `infra/otel-collector/otel-collector-config.yaml` with OTLP receiver, batch processor, and Prometheus/Loki/debug exporters
- [x] Add Prometheus service to `docker-compose.yml` and create `infra/prometheus/prometheus.yml` with scrape targets for API and collector
- [x] Add Loki service to `docker-compose.yml` and create `infra/loki/loki-config.yaml` with local filesystem storage
- [x] Add Grafana service to `docker-compose.yml` with provisioning volume mounts and environment config
- [x] Create Grafana provisioning files: datasources (Prometheus + Loki) and dashboard provider config
- [x] Create `propeliq-overview.json` Grafana dashboard with request rate, error rate, latency p50/p95/p99, and log stream panels
- [x] Add `GRAFANA_ADMIN_PASSWORD` to `.env.example` and add `prometheus-data`, `grafana-data`, `loki-data` named volumes

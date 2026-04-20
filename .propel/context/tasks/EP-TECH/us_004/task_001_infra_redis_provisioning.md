# Task - TASK_001

## Requirement Reference

- User Story: us_004
- Story Location: .propel/context/tasks/EP-TECH/us_004/us_004.md
- Acceptance Criteria:
  - AC-1: Given the Docker Compose stack is running, When the Redis container starts, Then the cache is accessible at the configured host and port with a ping confirming connectivity.
- Edge Case:
  - N/A (infrastructure provisioning task; application-level edge cases addressed in task_002)

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
| Library | Docker / Docker Compose | latest stable |
| Library | Redis | 7.x |
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

Add a Redis 7.x service to the existing Docker Compose stack for local development. The container includes a health check probe using `redis-cli ping`, a restart policy for availability (NFR-005), environment-driven configuration, and a persistent volume for data durability across restarts. The Redis instance serves as the distributed cache backend for TR-004 hot slot search and profile read acceleration.

## Dependent Tasks

- US_005 tasks (Docker Compose environment) - If `docker-compose.yml` does not yet exist, this task creates the Redis service block. If `docker-compose.yml` already exists (from US_003/task_001), this task appends the Redis service.

## Impacted Components

- Modified/Created: `docker-compose.yml` (Redis service definition)
- Modified: `.env.example` (Redis host, port, and password placeholders)
- Modified: `server/src/PropelIQ.Api/appsettings.Development.json` (Redis connection string)

## Implementation Plan

1. **Add Redis service to Docker Compose** using the official `redis:7-alpine` image. Configure with:
   - Port mapping `6379:6379` (configurable via `.env`)
   - Named volume `redisdata` for persistence
   - Health check using `redis-cli ping` with 5s interval, 5s timeout, 5 retries
   - Restart policy `unless-stopped`
   - Optional password via `--requirepass` command argument sourced from `.env`

### Docker Compose Redis Service Reference

```yaml
services:
  redis:
    image: redis:7-alpine
    container_name: propeliq-redis
    command: redis-server --requirepass ${REDIS_PASSWORD:-propeliq_dev_pass} --maxmemory 256mb --maxmemory-policy allkeys-lru
    ports:
      - "${REDIS_PORT:-6379}:6379"
    volumes:
      - redisdata:/data
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD:-propeliq_dev_pass}", "ping"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 5s
    restart: unless-stopped

volumes:
  redisdata:
    driver: local
```

2. **Configure memory policy** with `--maxmemory 256mb` and `--maxmemory-policy allkeys-lru` to prevent unbounded growth and auto-evict least recently used keys when memory limit is reached.

3. **Update `.env.example`** with `REDIS_HOST`, `REDIS_PORT`, and `REDIS_PASSWORD` placeholders.

4. **Add Redis connection string** to `server/src/PropelIQ.Api/appsettings.Development.json` referencing the Docker-hosted Redis instance.

5. **Validate connectivity** by starting Docker Compose and confirming `redis-cli ping` returns `PONG`.

## Current Project State

```text
propelIQ/
├── .github/
├── .propel/
├── app/              (Angular SPA from US_001)
├── server/           (ASP.NET Core API from US_002)
├── docker-compose.yml (PostgreSQL service from US_003)
├── docker/
│   └── postgres/init/ (init scripts from US_003)
├── .env.example
├── BRD.md
├── README.md
└── .gitignore
```

> Assumes US_003/task_001 is completed and docker-compose.yml exists. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | docker-compose.yml | Add Redis service with health check, memory policy, and named volume |
| MODIFY | .env.example | Add REDIS_HOST, REDIS_PORT, REDIS_PASSWORD placeholders |
| MODIFY | server/src/PropelIQ.Api/appsettings.Development.json | Add Redis connection string for local Docker instance |

## External References

- Redis Docker official image: https://hub.docker.com/_/redis
- Redis 7 configuration: https://redis.io/docs/latest/operate/oss_and_stack/management/config/
- Redis `maxmemory-policy` eviction docs: https://redis.io/docs/latest/develop/reference/eviction/
- Docker Compose health check: https://docs.docker.com/compose/compose-file/05-services/#healthcheck

## Build Commands

```bash
# Start Redis container (alongside existing services)
docker compose up -d redis

# Verify container health
docker compose ps

# Test Redis connectivity
docker compose exec redis redis-cli -a propeliq_dev_pass ping

# Verify memory policy
docker compose exec redis redis-cli -a propeliq_dev_pass CONFIG GET maxmemory-policy

# Stop services
docker compose down
```

## Implementation Validation Strategy

- [ ] `docker compose up -d redis` starts the Redis 7 container without errors
- [ ] Container health check reports `healthy` within 20 seconds
- [ ] `redis-cli ping` returns `PONG` confirming connectivity
- [ ] `CONFIG GET maxmemory` returns `256mb`
- [ ] `CONFIG GET maxmemory-policy` returns `allkeys-lru`
- [ ] Named volume `redisdata` persists data across container restarts

## Implementation Checklist

- [x] Add Redis service to `docker-compose.yml` using `redis:7-alpine` with health check, restart policy, and named volume
- [x] Configure `--requirepass` and `--maxmemory 256mb --maxmemory-policy allkeys-lru` command arguments
- [x] Update `.env.example` with `REDIS_HOST`, `REDIS_PORT`, `REDIS_PASSWORD` placeholders
- [x] Add Redis connection string to `appsettings.Development.json`
- [ ] Start container and verify connectivity with `redis-cli ping`

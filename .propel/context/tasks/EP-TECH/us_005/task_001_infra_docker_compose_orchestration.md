# Task - TASK_001

## Requirement Reference

- User Story: us_005
- Story Location: .propel/context/tasks/EP-TECH/us_005/us_005.md
- Acceptance Criteria:
  - AC-1: Given Docker Desktop is installed, When `docker compose up` is executed from the project root, Then all services (API, Angular dev server, PostgreSQL, Redis) start within 2 minutes and pass their health checks.
  - AC-2: Given the Compose stack is up, When environment variables are loaded from `.env`, Then each service uses the configured values and no secrets are hardcoded in the Compose file.
  - AC-4: Given the stack is running, When `docker compose down` is executed, Then all containers stop cleanly and database volumes are preserved.
- Edge Case:
  - What happens if a required port is already bound by another process? Compose exits with a clear error message identifying the conflicting port.

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
| Database | PostgreSQL with pgvector | 15.x |
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

Create the Docker Compose orchestration file that defines all platform services (PostgreSQL, Redis, ASP.NET Core API, Angular dev server), configure environment variable loading from `.env` with no hardcoded secrets, set up health check probes for every service, define named volumes for database persistence across `docker compose down` cycles, configure service dependency ordering, and set restart policies for availability (NFR-005). This task consolidates the individual service definitions from US_003 and US_004 into a unified multi-service stack and adds the application service definitions.

## Dependent Tasks

- None (this task creates the foundational Docker Compose environment; US_003/task_001 and US_004/task_001 may have created partial docker-compose.yml — this task unifies and completes it)

## Impacted Components

- New/Modified: `docker-compose.yml` (unified multi-service stack)
- New/Modified: `.env.example` (consolidated environment variable template)
- Modified: `.gitignore` (ensure `.env` is excluded)
- New: `docker/postgres/init/01-create-extensions.sql` (if not already from US_003)
- New: `docker/postgres/init/02-create-schemas.sql` (if not already from US_003)

## Implementation Plan

1. **Create the unified `docker-compose.yml`** at project root with all four services. Each service definition includes health check, restart policy, named volume (where applicable), and `.env`-sourced configuration. Services use `depends_on` with `condition: service_healthy` to enforce startup ordering.

### Docker Compose Full Stack Definition

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg15
    container_name: propeliq-postgres
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-propeliq}
      POSTGRES_USER: ${POSTGRES_USER:-propeliq_user}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-propeliq_dev_pass}
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./docker/postgres/init:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-propeliq_user} -d ${POSTGRES_DB:-propeliq}"]
      interval: 5s
      timeout: 5s
      retries: 5
      start_period: 10s
    restart: unless-stopped

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

  api:
    build:
      context: ./server
      dockerfile: Dockerfile
    container_name: propeliq-api
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:5000
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB:-propeliq};Username=${POSTGRES_USER:-propeliq_user};Password=${POSTGRES_PASSWORD:-propeliq_dev_pass}"
      ConnectionStrings__Redis: "${REDIS_HOST:-redis}:${REDIS_PORT:-6379},password=${REDIS_PASSWORD:-propeliq_dev_pass}"
    ports:
      - "${API_PORT:-5000}:5000"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:5000/api/v1/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped
    volumes:
      - ./server/src:/src:ro

  angular:
    build:
      context: ./app
      dockerfile: Dockerfile.dev
    container_name: propeliq-angular
    environment:
      NODE_ENV: development
    ports:
      - "${ANGULAR_PORT:-4200}:4200"
    depends_on:
      api:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:4200 || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped
    volumes:
      - ./app/src:/app/src
      - /app/node_modules

volumes:
  pgdata:
    driver: local
  redisdata:
    driver: local
```

2. **Create consolidated `.env.example`** with all service configuration variables grouped by service. Include inline comments documenting each variable's purpose:

```env
# PostgreSQL
POSTGRES_DB=propeliq
POSTGRES_USER=propeliq_user
POSTGRES_PASSWORD=propeliq_dev_pass
POSTGRES_PORT=5432

# Redis
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=propeliq_dev_pass

# API
API_PORT=5000

# Angular
ANGULAR_PORT=4200
```

3. **Ensure `.gitignore` excludes `.env`** to prevent credential leakage. Verify `.env.example` is tracked.

4. **Verify dependency ordering** via `depends_on` with `condition: service_healthy` ensures PostgreSQL and Redis are healthy before API starts, and API is healthy before Angular starts.

5. **Verify volume preservation** by confirming `docker compose down` (without `-v`) retains `pgdata` and `redisdata` named volumes, and `docker compose down -v` removes them.

6. **Validate port conflict behavior** by confirming Docker Compose produces a clear error when a mapped port is already bound.

## Current Project State

```text
propelIQ/
├── .github/
├── .propel/
├── .vscode/
├── app/              (Angular SPA from US_001)
├── server/           (ASP.NET Core API from US_002)
├── docker/
│   └── postgres/init/ (may exist from US_003)
├── BRD.md
├── README.md
├── .gitignore
└── .env.example      (may exist from US_003/US_004)
```

> Placeholder: docker-compose.yml may partially exist from US_003/US_004 tasks. This task creates the definitive unified version.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | docker-compose.yml | Unified multi-service stack with PostgreSQL, Redis, API, Angular |
| CREATE | .env.example | Consolidated environment variable template for all services |
| MODIFY | .gitignore | Ensure `.env` is excluded from version control |
| CREATE | docker/postgres/init/01-create-extensions.sql | pgvector and uuid-ossp extension initialization (if not from US_003) |
| CREATE | docker/postgres/init/02-create-schemas.sql | Application and audit schema creation (if not from US_003) |

## External References

- Docker Compose specification: https://docs.docker.com/compose/compose-file/
- Docker Compose `depends_on` with health conditions: https://docs.docker.com/compose/compose-file/05-services/#depends_on
- Docker Compose environment variables: https://docs.docker.com/compose/environment-variables/
- Docker Compose named volumes: https://docs.docker.com/compose/compose-file/07-volumes/
- pgvector/pgvector Docker image: https://hub.docker.com/r/pgvector/pgvector
- Redis Docker image: https://hub.docker.com/_/redis

## Build Commands

```bash
# Start all services
docker compose up -d

# View service status and health
docker compose ps

# View logs for all services
docker compose logs -f

# Stop all services (volumes preserved)
docker compose down

# Stop all services and remove volumes
docker compose down -v
```

## Implementation Validation Strategy

- [ ] `docker compose up -d` starts all 4 services within 2 minutes
- [ ] `docker compose ps` shows all services as `healthy`
- [ ] No secrets are hardcoded in `docker-compose.yml` (all sourced from `.env` or defaults)
- [ ] `.env.example` contains all required variables with documented defaults
- [ ] `docker compose down` stops all containers; `pgdata` and `redisdata` volumes persist
- [ ] `docker compose down -v` removes named volumes
- [ ] Port conflict produces clear Docker Compose error identifying the conflicting port

## Implementation Checklist

- [x] Create `docker-compose.yml` with PostgreSQL, Redis, API, and Angular service definitions including health checks and restart policies
- [x] Configure `depends_on` with `condition: service_healthy` for startup ordering (postgres/redis -> api -> angular)
- [x] Create consolidated `.env.example` with all service variables grouped and commented
- [x] Ensure `.env` is in `.gitignore` and `.env.example` is tracked
- [x] Create PostgreSQL init scripts in `docker/postgres/init/` if not already present from US_003
- [x] Define `pgdata` and `redisdata` named volumes for data persistence across restarts
- [ ] Validate all 4 services start and pass health checks within 2 minutes
- [ ] Verify `docker compose down` preserves volumes while `docker compose down -v` removes them

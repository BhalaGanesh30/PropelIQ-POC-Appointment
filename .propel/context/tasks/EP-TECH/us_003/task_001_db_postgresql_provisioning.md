# Task - TASK_001

## Requirement Reference

- User Story: us_003
- Story Location: .propel/context/tasks/EP-TECH/us_003/us_003.md
- Acceptance Criteria:
  - AC-1: Given the Docker Compose stack is started, When the PostgreSQL 15 container initializes, Then the database is accessible on the configured port with the application credentials.
  - AC-2: Given the PostgreSQL instance is running, When `CREATE EXTENSION IF NOT EXISTS vector;` is executed, Then the pgvector extension is active and `SELECT * FROM pg_extension WHERE extname = 'vector'` returns a row.
- Edge Case:
  - What happens if the database port is already in use? Docker Compose fails with a port conflict error; developer resolves by changing the port mapping in `docker-compose.yml`.

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
| AI/ML | N/A | N/A |
| Vector Store | pgvector | 0.7.x |
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

Provision a PostgreSQL 15 database instance with the pgvector extension enabled via Docker Compose for local development. The container uses the `pgvector/pgvector:pg15` image which bundles pgvector with PostgreSQL 15. An initialization SQL script automatically creates the `vector` extension on first startup. The service includes a health check probe, restart policy, persistent volume for data durability, and environment-driven credential configuration. This task delivers the primary datastore foundation required by TR-003 and DR-001.

## Dependent Tasks

- US_005 tasks (Docker Compose environment) - Foundational dependency per user story. If US_005 tasks are not yet complete, this task creates the initial `docker-compose.yml` with the PostgreSQL service, which US_005 tasks will extend with additional services.

## Impacted Components

- New/Modified: `docker-compose.yml` (PostgreSQL service definition)
- New: `docker/postgres/init/01-create-extensions.sql` (pgvector extension initialization)
- New: `docker/postgres/init/02-create-schemas.sql` (application schema setup)
- New: `.env.example` (database credential placeholders)
- Modified: `.gitignore` (exclude `.env` file)

## Implementation Plan

1. **Create the Docker Compose PostgreSQL service** using the `pgvector/pgvector:pg15` image. This image bundles PostgreSQL 15 with pgvector pre-installed, eliminating the need for manual extension compilation. Configure the service with:
   - Environment variables for `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` sourced from `.env` file
   - Port mapping `5432:5432` (configurable via `.env`)
   - Named volume `pgdata` for data persistence across container restarts
   - Restart policy `unless-stopped` for NFR-005 availability
   - Health check using `pg_isready` command with 5s interval, 5s timeout, 5 retries

### Docker Compose PostgreSQL + pgvector Service Reference

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

volumes:
  pgdata:
    driver: local
```

Source: Docker Hub pgvector/pgvector image documentation

2. **Create initialization SQL scripts** in `docker/postgres/init/` directory. PostgreSQL's official Docker image executes `.sql` files in `/docker-entrypoint-initdb.d/` alphabetically on first container start:
   - `01-create-extensions.sql`: Enables `vector`, `uuid-ossp`, and `pg_trgm` extensions
   - `02-create-schemas.sql`: Creates the `app` schema for application tables and `audit` schema for audit tables

3. **Create `.env.example`** with placeholder credentials and port configuration. Add `.env` to `.gitignore` to prevent credential leakage.

4. **Add connection string configuration** to `server/src/PropelIQ.Api/appsettings.Development.json` referencing the Docker-hosted database.

5. **Validate the provisioning** by starting Docker Compose, connecting to the database, and confirming pgvector extension is active via `pg_extension` query.

## Current Project State

```text
propelIQ/
├── .github/
├── .propel/
├── .vscode/
├── app/              (Angular SPA from US_001)
├── server/           (ASP.NET Core API from US_002)
├── BRD.md
├── README.md
└── .gitignore
```

> Placeholder: No Docker Compose or database configuration exists. This task creates the initial docker-compose.yml and database provisioning scripts.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | docker-compose.yml | Docker Compose file with PostgreSQL + pgvector service definition |
| CREATE | docker/postgres/init/01-create-extensions.sql | SQL script enabling vector, uuid-ossp, and pg_trgm extensions |
| CREATE | docker/postgres/init/02-create-schemas.sql | SQL script creating app and audit schemas |
| CREATE | .env.example | Template with POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_PORT placeholders |
| MODIFY | .gitignore | Add .env to ignored files |
| MODIFY | server/src/PropelIQ.Api/appsettings.Development.json | Add PostgreSQL connection string for local Docker instance |

## External References

- pgvector/pgvector Docker image: https://hub.docker.com/r/pgvector/pgvector
- pgvector GitHub (extension docs): https://github.com/pgvector/pgvector
- PostgreSQL 15 Docker initialization: https://hub.docker.com/_/postgres (Initialization scripts section)
- Docker Compose health checks: https://docs.docker.com/compose/compose-file/05-services/#healthcheck
- PostgreSQL `pg_isready` utility: https://www.postgresql.org/docs/15/app-pg-isready.html

## Build Commands

```bash
# Start PostgreSQL container
docker compose up -d postgres

# Verify container health
docker compose ps

# Connect and verify pgvector extension
docker compose exec postgres psql -U propeliq_user -d propeliq -c "SELECT * FROM pg_extension WHERE extname = 'vector';"

# Stop and clean up
docker compose down

# Full reset (including data)
docker compose down -v
```

## Implementation Validation Strategy

- [ ] `docker compose up -d postgres` starts the PostgreSQL 15 container without errors
- [ ] Container health check reports `healthy` within 30 seconds
- [ ] Connection with application credentials succeeds: `psql -U propeliq_user -d propeliq`
- [ ] `SELECT * FROM pg_extension WHERE extname = 'vector'` returns a row confirming pgvector is active
- [ ] `SELECT * FROM pg_extension WHERE extname = 'uuid-ossp'` returns a row
- [ ] Named volume `pgdata` persists data across container restarts (`docker compose down` + `docker compose up -d`)
- [ ] Port conflict scenario produces a clear Docker Compose error message

## Implementation Checklist

- [x] Create `docker-compose.yml` with PostgreSQL service using `pgvector/pgvector:pg15` image, health check, restart policy, and named volume
- [x] Create `docker/postgres/init/01-create-extensions.sql` enabling `vector`, `uuid-ossp`, and `pg_trgm` extensions
- [x] Create `docker/postgres/init/02-create-schemas.sql` creating `app` and `audit` schemas
- [x] Create `.env.example` with database credential placeholders and add `.env` to `.gitignore`
- [x] Add PostgreSQL connection string to `server/src/PropelIQ.Api/appsettings.Development.json`
- [ ] Start container and verify pgvector extension is active via `pg_extension` query
- [ ] Verify data persistence across container restart cycles

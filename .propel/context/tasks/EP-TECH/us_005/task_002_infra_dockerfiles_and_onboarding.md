# Task - TASK_002

## Requirement Reference

- User Story: us_005
- Story Location: .propel/context/tasks/EP-TECH/us_005/us_005.md
- Acceptance Criteria:
  - AC-1: Given Docker Desktop is installed, When `docker compose up` is executed from the project root, Then all services (API, Angular dev server, PostgreSQL, Redis) start within 2 minutes and pass their health checks.
  - AC-3: Given the stack is running, When a developer makes a code change in the Angular project, Then hot module replacement reflects the change in the browser within 5 seconds.
- Edge Case:
  - How does the team onboard a new developer? `README.md` includes a quickstart section with the single `docker compose up` command and expected output.

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
| Frontend | Angular | 17.x |
| Backend | ASP.NET Core Web API | 8.x |
| Database | N/A | N/A |
| Library | Docker / Docker Compose | latest stable |
| Library | Node.js | 20.x LTS |
| Library | .NET SDK | 8.x |
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

Create development Dockerfiles for the Angular SPA and ASP.NET Core API services with live-reload support. The Angular Dockerfile uses `ng serve` with `--poll` for file-system change detection inside Docker, enabling hot module replacement (HMR) that reflects code changes within 5 seconds. The API Dockerfile uses `dotnet watch run` for automatic recompilation on C# file changes. Both Dockerfiles are optimized for development speed (not production). Additionally, update `README.md` with a quickstart onboarding section documenting the single-command `docker compose up` workflow for new developers.

## Dependent Tasks

- task_001_infra_docker_compose_orchestration (requires docker-compose.yml with service definitions referencing these Dockerfiles)
- US_001 tasks (requires Angular project scaffold in `app/`)
- US_002 tasks (requires ASP.NET Core project in `server/`)

## Impacted Components

- New: `app/Dockerfile.dev` (Angular development Dockerfile with HMR)
- New: `app/.dockerignore` (exclude node_modules and dist from build context)
- New: `server/Dockerfile` (ASP.NET Core development Dockerfile with dotnet watch)
- New: `server/.dockerignore` (exclude bin/obj from build context)
- Modified: `README.md` (quickstart onboarding section)

## Implementation Plan

1. **Create `app/Dockerfile.dev`** for the Angular development server using Node.js 20 LTS base image. Mount source via Docker Compose volume for live file watching. Configure `ng serve` with `--host 0.0.0.0` to bind to all interfaces inside the container and `--poll 1000` for file-system polling (required for Docker volume mounts on Windows/macOS where native inotify events are not forwarded):

### Angular Development Dockerfile

```dockerfile
FROM node:20-alpine AS dev

WORKDIR /app

# Install dependencies first for Docker layer caching
COPY package.json package-lock.json ./
RUN npm ci

# Copy source (overridden by volume mount in docker-compose)
COPY . .

EXPOSE 4200

# --poll enables file change detection in Docker volumes
# --host binds to all interfaces for container access
CMD ["npx", "ng", "serve", "--host", "0.0.0.0", "--poll", "1000", "--disable-host-check"]
```

2. **Create `app/.dockerignore`** to exclude `node_modules`, `dist`, `.angular`, and `.git` from the build context, reducing image build time.

3. **Create `server/Dockerfile`** for the ASP.NET Core API using the .NET 8 SDK base image. Use `dotnet watch run` for automatic recompilation on file changes:

### ASP.NET Core Development Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev

WORKDIR /src

# Restore dependencies first for layer caching
COPY PropelIQ.sln ./
COPY src/PropelIQ.Api/PropelIQ.Api.csproj src/PropelIQ.Api/
COPY src/PropelIQ.SharedKernel/PropelIQ.SharedKernel.csproj src/PropelIQ.SharedKernel/
COPY src/Modules/ src/Modules/
RUN dotnet restore PropelIQ.sln

# Copy remaining source
COPY . .

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

WORKDIR /src/src/PropelIQ.Api
CMD ["dotnet", "watch", "run", "--no-launch-profile"]
```

4. **Create `server/.dockerignore`** to exclude `bin/`, `obj/`, `.git`, and publish artifacts from the build context.

5. **Ensure Docker Compose volume mounts enable HMR**. The `angular` service in `docker-compose.yml` mounts `./app/src:/app/src` so source file changes on the host propagate into the container. The anonymous volume `/app/node_modules` prevents the host mount from overwriting installed dependencies.

6. **Validate HMR** by starting the stack, modifying a TypeScript file in `app/src/`, and confirming the browser reflects the change within 5 seconds without manual refresh.

7. **Update `README.md`** with a quickstart onboarding section:

### README Quickstart Section

```markdown
## Quickstart

### Prerequisites
- Docker Desktop (v4.x+)

### Start the Platform
```bash
cp .env.example .env
docker compose up -d
```

### Access Services
| Service | URL |
|---------|-----|
| Angular App | http://localhost:4200 |
| API | http://localhost:5000 |
| API Health | http://localhost:5000/api/v1/health |

### Verify Stack Health
```bash
docker compose ps
```
All services should show `healthy` status within 2 minutes.

### Stop Services
```bash
# Preserve data
docker compose down

# Reset everything
docker compose down -v
```
```

8. **Validate end-to-end** by following the README quickstart from a clean state and confirming all services start and pass health checks.

## Current Project State

```text
propelIQ/
├── .github/
├── .propel/
├── app/              (Angular SPA from US_001)
├── server/           (ASP.NET Core API from US_002)
├── docker-compose.yml (from task_001)
├── docker/
│   └── postgres/init/
├── .env.example
├── BRD.md
├── README.md
└── .gitignore
```

> Assumes task_001 is completed with docker-compose.yml referencing `app/Dockerfile.dev` and `server/Dockerfile`. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | app/Dockerfile.dev | Angular 17 development Dockerfile with Node 20 LTS, ng serve, and poll-based HMR |
| CREATE | app/.dockerignore | Exclude node_modules, dist, .angular, .git from build context |
| CREATE | server/Dockerfile | ASP.NET Core 8 development Dockerfile with dotnet watch run |
| CREATE | server/.dockerignore | Exclude bin, obj, .git from build context |
| MODIFY | README.md | Add quickstart onboarding section with docker compose up workflow |

## External References

- Docker multi-stage builds: https://docs.docker.com/build/building/multi-stage/
- Angular CLI `ng serve` options: https://angular.io/cli/serve
- Angular file polling for Docker: `--poll` flag for volume-mounted file watchers
- ASP.NET Core `dotnet watch`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch
- DOTNET_USE_POLLING_FILE_WATCHER env var: https://learn.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-8.0
- Docker .dockerignore: https://docs.docker.com/build/building/context/#dockerignore-files
- Node.js 20 LTS Docker image: https://hub.docker.com/_/node

## Build Commands

```bash
# Build and start all services
docker compose up -d --build

# Rebuild a single service
docker compose build angular
docker compose build api

# View Angular logs (HMR output)
docker compose logs -f angular

# View API logs (dotnet watch output)
docker compose logs -f api
```

## Implementation Validation Strategy

- [ ] `docker compose up -d --build` builds both Dockerfiles and starts all services
- [ ] Angular container serves at `http://localhost:4200`
- [ ] API container serves at `http://localhost:5000`
- [ ] Modifying a TypeScript file in `app/src/` triggers HMR and reflects in browser within 5 seconds
- [ ] Modifying a C# file in `server/src/` triggers `dotnet watch` recompilation
- [ ] `README.md` quickstart section is complete and accurate
- [ ] New developer can onboard using only `cp .env.example .env && docker compose up -d`

## Implementation Checklist

- [ ] Create `app/Dockerfile.dev` with Node 20 LTS, npm ci, and `ng serve --host 0.0.0.0 --poll 1000`
- [ ] Create `app/.dockerignore` excluding node_modules, dist, .angular, .git
- [ ] Create `server/Dockerfile` with .NET 8 SDK, dotnet restore, and `dotnet watch run` with `DOTNET_USE_POLLING_FILE_WATCHER=true`
- [ ] Create `server/.dockerignore` excluding bin, obj, .git
- [ ] Verify Angular HMR reflects source changes within 5 seconds in Docker
- [ ] Verify API hot-reload recompiles on C# file changes
- [ ] Update `README.md` with quickstart section (prerequisites, start, access URLs, health check, stop)
- [ ] End-to-end validation: clean clone -> cp .env.example .env -> docker compose up -> all services healthy

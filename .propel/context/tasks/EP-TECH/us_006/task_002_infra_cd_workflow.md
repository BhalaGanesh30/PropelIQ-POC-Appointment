# Task - TASK_002

## Requirement Reference

- User Story: us_006
- Story Location: .propel/context/tasks/EP-TECH/us_006/us_006.md
- Acceptance Criteria:
  - AC-2: Given all CI checks pass on the main branch, When the CD stage triggers, Then the application is deployed to the staging environment within 15 minutes.
  - AC-3: Given the deployment pipeline is configured, When a deployment to production is initiated, Then a manual approval gate is required before the production deployment proceeds.
  - AC-4: Given any pipeline stage fails, When the failure is detected, Then the pipeline stops, the error is surfaced in the workflow summary, and no deployment occurs.
- Edge Case:
  - What happens if secrets are missing from the GitHub Actions environment? Pipeline fails with a descriptive error; no deployment proceeds with incomplete credentials.

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
| Library | GitHub Actions | latest stable |
| Library | Docker | latest stable |
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

Create the GitHub Actions CD workflow that triggers after successful CI on the `main` branch. The workflow builds production Docker images for the Angular SPA and ASP.NET Core API, pushes them to a container registry, deploys automatically to the staging environment, and requires a manual approval gate before promoting to production. Each deployment stage includes health check verification after deployment. Failure at any stage stops the pipeline and surfaces the error in the workflow summary. The staging deployment completes within 15 minutes. This task delivers the gated deployment pipeline required by the design.md technology stack (GitHub Actions with gated environments) and NFR-005/NFR-011.

## Dependent Tasks

- task_001_infra_ci_workflow (CI must pass before CD triggers)
- US_001 tasks (requires Angular project for Docker image build)
- US_002 tasks (requires ASP.NET Core project for Docker image build)
- US_005 task_002 (requires Dockerfiles for image builds)

## Impacted Components

- New: `.github/workflows/cd.yml` (CD workflow definition)
- New: `app/Dockerfile` (Angular production multi-stage Dockerfile)
- New: `server/Dockerfile.prod` (ASP.NET Core production multi-stage Dockerfile)

## Implementation Plan

1. **Create `.github/workflows/cd.yml`** triggered on successful completion of the CI workflow on `main` branch using `workflow_run` trigger. This ensures CD only runs after CI passes:

### CD Workflow Trigger

```yaml
name: CD

on:
  workflow_run:
    workflows: ["CI"]
    branches: [main]
    types: [completed]

concurrency:
  group: cd-${{ github.ref }}
  cancel-in-progress: false

permissions:
  contents: read
  packages: write
  id-token: write
```

2. **Create the build-and-push job** that builds production Docker images and pushes to GitHub Container Registry (ghcr.io):

### Docker Build and Push

```yaml
jobs:
  build-images:
    runs-on: ubuntu-latest
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    outputs:
      image-tag: ${{ steps.meta.outputs.tags }}
    steps:
      - uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push API image
        uses: docker/build-push-action@v6
        with:
          context: ./server
          file: ./server/Dockerfile.prod
          push: true
          tags: ghcr.io/${{ github.repository }}/api:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Build and push Angular image
        uses: docker/build-push-action@v6
        with:
          context: ./app
          file: ./app/Dockerfile
          push: true
          tags: ghcr.io/${{ github.repository }}/angular:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

3. **Create the staging deployment job** using a GitHub Actions `environment: staging`. This job runs after successful image build, deploys to the staging target, and verifies deployment health. The staging environment does not require manual approval — it auto-deploys on every successful CI:

```yaml
  deploy-staging:
    runs-on: ubuntu-latest
    needs: build-images
    environment:
      name: staging
      url: ${{ vars.STAGING_URL }}
    steps:
      - name: Deploy to staging
        run: |
          echo "Deploying images to staging environment..."
          # Placeholder: Replace with actual deployment command
          # e.g., docker compose pull && docker compose up -d
          # or platform-specific CLI (Render, Railway, etc.)

      - name: Verify staging health
        run: |
          echo "Waiting for staging health check..."
          for i in $(seq 1 30); do
            if curl -sf "${{ vars.STAGING_URL }}/api/v1/health" > /dev/null 2>&1; then
              echo "Staging is healthy"
              exit 0
            fi
            sleep 10
          done
          echo "::error::Staging health check failed after 5 minutes"
          exit 1
```

4. **Create the production deployment job** using a GitHub Actions `environment: production` with **required reviewers** configured as the manual approval gate. This environment protection rule must be configured in the GitHub repository settings (Settings > Environments > production > Required reviewers). The job runs only after staging deployment succeeds:

### Production Deployment with Manual Approval Gate

```yaml
  deploy-production:
    runs-on: ubuntu-latest
    needs: deploy-staging
    environment:
      name: production
      url: ${{ vars.PRODUCTION_URL }}
    steps:
      - name: Deploy to production
        run: |
          echo "Deploying images to production environment..."
          # Placeholder: Replace with actual deployment command

      - name: Verify production health
        run: |
          echo "Waiting for production health check..."
          for i in $(seq 1 30); do
            if curl -sf "${{ vars.PRODUCTION_URL }}/api/v1/health" > /dev/null 2>&1; then
              echo "Production is healthy"
              exit 0
            fi
            sleep 10
          done
          echo "::error::Production health check failed after 5 minutes"
          exit 1

      - name: Write deployment summary
        if: success()
        run: |
          echo "## Production Deployment Complete" >> $GITHUB_STEP_SUMMARY
          echo "- **Image Tag**: ${{ github.sha }}" >> $GITHUB_STEP_SUMMARY
          echo "- **Environment**: production" >> $GITHUB_STEP_SUMMARY
          echo "- **URL**: ${{ vars.PRODUCTION_URL }}" >> $GITHUB_STEP_SUMMARY
```

5. **Create production multi-stage Dockerfiles**:

### Angular Production Dockerfile (`app/Dockerfile`)

```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

FROM nginx:alpine AS production
COPY --from=build /app/dist/*/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### ASP.NET Core Production Dockerfile (`server/Dockerfile.prod`)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PropelIQ.sln ./
COPY src/ src/
RUN dotnet restore PropelIQ.sln
RUN dotnet publish src/PropelIQ.Api/PropelIQ.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS production
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "PropelIQ.Api.dll"]
```

6. **Add failure handling** using `if: failure()` step at the end of each job that writes the failure details to `$GITHUB_STEP_SUMMARY` and ensures no downstream jobs execute.

7. **Configure GitHub repository environments** (documented in README or setup guide):
   - `staging`: No approval required, auto-deploy on CI pass
   - `production`: Required reviewers enabled (manual approval gate), deployment branch restricted to `main`

8. **Set timeout constraints**: `timeout-minutes: 15` on each deployment job.

## Current Project State

```text
propelIQ/
├── .github/
│   ├── workflows/
│   │   └── ci.yml        (from task_001)
│   ├── instructions/
│   └── prompts/
├── app/
│   ├── Dockerfile.dev     (from US_005)
│   └── (Angular project)
├── server/
│   ├── Dockerfile         (dev Dockerfile from US_005)
│   └── (ASP.NET Core project)
├── docker-compose.yml
└── README.md
```

> Assumes task_001 and US_005 tasks are completed. Update on execution if structure differs.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | .github/workflows/cd.yml | CD workflow with build-images, deploy-staging, deploy-production jobs |
| CREATE | app/Dockerfile | Angular production multi-stage Dockerfile (Node build + nginx serve) |
| CREATE | app/nginx.conf | Nginx configuration for Angular SPA routing (try_files for client-side routes) |
| CREATE | server/Dockerfile.prod | ASP.NET Core production multi-stage Dockerfile (SDK build + aspnet runtime) |

## External References

- GitHub Actions workflow_run trigger: https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows#workflow_run
- GitHub Actions environments and protection rules: https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment
- GitHub Actions manual approval: https://docs.github.com/en/actions/managing-workflow-runs/reviewing-deployments
- docker/build-push-action: https://github.com/docker/build-push-action
- docker/login-action: https://github.com/docker/login-action
- GitHub Container Registry (ghcr.io): https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry
- Docker multi-stage builds: https://docs.docker.com/build/building/multi-stage/
- Nginx SPA configuration: https://nginx.org/en/docs/beginners_guide.html
- GITHUB_STEP_SUMMARY: https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary

## Build Commands

```bash
# CD triggers automatically after CI passes on main
# Manual trigger for testing:
gh workflow run cd.yml

# View deployment status
gh run list --workflow=cd.yml

# View pending approvals
gh run view <run-id>

# Approve production deployment
gh run review <run-id> --approve
```

## Implementation Validation Strategy

- [ ] CD workflow triggers only after successful CI on `main`
- [ ] CD workflow does NOT trigger if CI fails (checks `workflow_run.conclusion == 'success'`)
- [ ] Docker images build and push to ghcr.io with commit SHA tags
- [ ] Staging deployment job completes within 15 minutes
- [ ] Staging health check verifies API is responsive after deployment
- [ ] Production deployment job pauses and waits for manual approval
- [ ] Production deployment proceeds only after reviewer approves
- [ ] Any stage failure stops the pipeline and surfaces error in workflow summary

## Implementation Checklist

- [x] Create `.github/workflows/cd.yml` with `workflow_run` trigger gated on CI success
- [x] Create `build-images` job: Docker login to ghcr.io, build and push API and Angular production images with GHA cache
- [x] Create `deploy-staging` job with `environment: staging`, deployment command placeholder, and health check verification
- [x] Create `deploy-production` job with `environment: production` (manual approval gate) and health check verification
- [x] Create `app/Dockerfile` (production multi-stage: Node build + nginx serve) and `app/nginx.conf` for SPA routing
- [x] Create `server/Dockerfile.prod` (production multi-stage: SDK build + aspnet runtime)
- [x] Add failure reporting to `$GITHUB_STEP_SUMMARY` on each job with `if: failure()` step
- [ ] Document GitHub environment setup (staging: auto-deploy, production: required reviewers) in repository configuration

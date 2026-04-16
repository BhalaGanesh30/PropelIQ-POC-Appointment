# Task - TASK_001

## Requirement Reference

- User Story: us_006
- Story Location: .propel/context/tasks/EP-TECH/us_006/us_006.md
- Acceptance Criteria:
  - AC-1: Given a pull request is opened, When the CI workflow triggers, Then the pipeline executes build, lint, unit tests, and integration tests and reports pass/fail status on the PR within 10 minutes.
  - AC-4: Given any pipeline stage fails, When the failure is detected, Then the pipeline stops, the error is surfaced in the workflow summary, and no deployment occurs.
- Edge Case:
  - What happens if secrets are missing from the GitHub Actions environment? Pipeline fails with a descriptive error; no deployment proceeds with incomplete credentials.
  - How does the pipeline handle flaky tests? Test retries are configured; after 3 consecutive failures the job is marked failed and alerts the team.

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
| Database | PostgreSQL with pgvector | 15.x |
| Library | GitHub Actions | latest stable |
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

Create the GitHub Actions CI workflow that triggers on pull requests targeting the `main` branch. The workflow runs parallel jobs for the frontend (Angular build + lint + unit tests) and backend (ASP.NET Core build + lint + unit tests + integration tests with a PostgreSQL service container). Each job uses fail-fast behavior so any stage failure stops the pipeline, surfaces the error in the GitHub workflow summary, and blocks the PR merge. Test retries handle flaky tests (3 attempts), and a secrets validation step fails early with a descriptive message if required credentials are missing. The entire CI run completes within 10 minutes.

## Dependent Tasks

- US_001 tasks (requires Angular project with build and test scripts)
- US_002 tasks (requires ASP.NET Core solution with compilable projects)

## Impacted Components

- New: `.github/workflows/ci.yml` (CI workflow definition)
- New: `.github/actions/setup-dotnet/action.yml` (optional composite action for .NET setup reuse)
- New: `.github/actions/setup-node/action.yml` (optional composite action for Node.js setup reuse)

## Implementation Plan

1. **Create `.github/workflows/ci.yml`** triggered on `pull_request` events targeting `main` branch and on `push` to `main`. Use `concurrency` group to cancel in-progress runs when a new commit is pushed to the same PR branch.

### CI Workflow Structure

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read
  checks: write
  pull-requests: write
```

2. **Create the secrets validation job** that runs first and verifies all required secrets exist. If any secret is missing, the job fails with a descriptive error message in the workflow summary and all downstream jobs are skipped:

```yaml
jobs:
  validate-secrets:
    runs-on: ubuntu-latest
    steps:
      - name: Validate required secrets
        run: |
          missing=""
          if [ -z "${{ secrets.DEPLOY_TOKEN }}" ]; then missing="$missing DEPLOY_TOKEN"; fi
          if [ -z "${{ secrets.REGISTRY_URL }}" ]; then missing="$missing REGISTRY_URL"; fi
          if [ -n "$missing" ]; then
            echo "::error::Missing required secrets:$missing"
            exit 1
          fi
```

3. **Create the frontend CI job** (`ci-frontend`) with the following steps:
   - Checkout code
   - Set up Node.js 20.x with npm cache
   - `npm ci` (deterministic dependency install)
   - `ng lint` (ESLint/Angular lint)
   - `ng build --configuration production` (AOT build verification)
   - `ng test --no-watch --code-coverage --browsers=ChromeHeadless` (unit tests with coverage)
   - Upload test results and coverage as workflow artifacts

4. **Create the backend CI job** (`ci-backend`) with PostgreSQL service container for integration tests:
   - Checkout code
   - Set up .NET 8 SDK with NuGet cache
   - `dotnet build server/PropelIQ.sln --configuration Release` (build verification)
   - `dotnet format server/PropelIQ.sln --verify-no-changes` (code style lint)
   - `dotnet test server/PropelIQ.sln --configuration Release --no-build` (unit tests)
   - Integration tests with PostgreSQL service container running `pgvector/pgvector:pg15`
   - Test retry configuration: 3 attempts for flaky tests using `dotnet test --blame-hang-timeout 60s`

### PostgreSQL Service Container for Integration Tests

```yaml
ci-backend:
  runs-on: ubuntu-latest
  needs: validate-secrets
  services:
    postgres:
      image: pgvector/pgvector:pg15
      env:
        POSTGRES_DB: propeliq_test
        POSTGRES_USER: test_user
        POSTGRES_PASSWORD: test_pass
      ports:
        - 5432:5432
      options: >-
        --health-cmd "pg_isready -U test_user -d propeliq_test"
        --health-interval 5s
        --health-timeout 5s
        --health-retries 5
```

5. **Configure fail-fast behavior** so that any step failure stops the job and surfaces the error:
   - Default `set -e` behavior in bash steps
   - `continue-on-error: false` (default) on all steps
   - Use `if: failure()` on a final summary step to write failure details to `$GITHUB_STEP_SUMMARY`

6. **Configure test retry for flaky tests** using the `nick-fields/retry@v3` action wrapping test commands with `max_attempts: 3` and `timeout_minutes: 5`:

```yaml
- name: Run unit tests (with retry)
  uses: nick-fields/retry@v3
  with:
    max_attempts: 3
    timeout_minutes: 5
    command: dotnet test server/PropelIQ.sln --configuration Release --no-build
```

7. **Add workflow summary reporting** using `$GITHUB_STEP_SUMMARY` to surface build times, test counts, and coverage percentage in the PR check output.

8. **Set timeout constraints** to ensure the entire workflow completes within 10 minutes: `timeout-minutes: 10` on each job.

## Current Project State

```text
propelIQ/
├── .github/
│   ├── instructions/   (coding standards)
│   └── prompts/        (workflow prompts)
├── .propel/
├── app/              (Angular SPA from US_001)
├── server/           (ASP.NET Core API from US_002)
├── docker-compose.yml
├── .env.example
├── BRD.md
├── README.md
└── .gitignore
```

> Placeholder: No CI/CD workflows exist. This task creates the initial CI workflow.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | .github/workflows/ci.yml | CI workflow with validate-secrets, ci-frontend, and ci-backend jobs |

## External References

- GitHub Actions workflow syntax: https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions
- GitHub Actions service containers: https://docs.github.com/en/actions/using-containerized-services/about-service-containers
- GitHub Actions concurrency groups: https://docs.github.com/en/actions/using-jobs/using-concurrency
- GitHub Actions environment protection: https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment
- nick-fields/retry action: https://github.com/nick-fields/retry
- Angular CI testing (ChromeHeadless): https://angular.io/guide/testing#set-up-continuous-integration
- dotnet test CLI: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- dotnet format (lint): https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
- GITHUB_STEP_SUMMARY: https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary

## Build Commands

```bash
# CI runs automatically on PR; manual trigger for testing:
gh workflow run ci.yml --ref feature-branch

# View workflow runs
gh run list --workflow=ci.yml

# View specific run details
gh run view <run-id>
```

## Implementation Validation Strategy

- [ ] CI workflow triggers on pull request to `main`
- [ ] CI workflow triggers on push to `main`
- [ ] Secrets validation job fails with descriptive error when secrets are missing
- [ ] Frontend job: Angular builds, lints, and tests pass
- [ ] Backend job: ASP.NET Core builds, lints, and tests pass with PostgreSQL service container
- [ ] Flaky test retry executes up to 3 attempts before marking failed
- [ ] Any step failure stops the pipeline and surfaces error in workflow summary
- [ ] Total CI execution completes within 10 minutes

## Implementation Checklist

- [ ] Create `.github/workflows/ci.yml` with `pull_request` and `push` triggers on `main` branch
- [ ] Add `concurrency` group with `cancel-in-progress: true` for duplicate run prevention
- [ ] Create `validate-secrets` job that fails early with descriptive error on missing secrets
- [ ] Create `ci-frontend` job: checkout, Node 20, npm ci, ng lint, ng build, ng test with ChromeHeadless
- [ ] Create `ci-backend` job: checkout, .NET 8 SDK, dotnet build, dotnet format, dotnet test with PostgreSQL service container
- [ ] Configure test retry (3 attempts) using `nick-fields/retry@v3` action
- [ ] Add `$GITHUB_STEP_SUMMARY` reporting for build/test results
- [ ] Set `timeout-minutes: 10` on each job to enforce 10-minute completion constraint

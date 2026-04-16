# Task - TASK_001

## Requirement Reference

- User Story: us_001
- Story Location: .propel/context/tasks/EP-TECH/us_001/us_001.md
- Acceptance Criteria:
  - AC-1: Given the project repository is cloned, When `npm install && ng serve` is run, Then the Angular 17 application compiles without errors and a routing shell renders at `http://localhost:4200`.
  - AC-4: Given the project scaffold is in place, When `ng build --configuration production` is executed, Then the build completes successfully with AOT compilation and no TypeScript strict-mode errors.
- Edge Case:
  - N/A (scaffold-level task; edge cases addressed in task_003)

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
| Backend | N/A | N/A |
| Database | N/A | N/A |
| Library | Angular CLI | 17.x |
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

Initialize the Angular 17 SPA workspace with Angular CLI, configure TypeScript strict mode, establish the modular project folder structure aligned with the layered architecture (TR-001), set up environment configuration files, and verify that both development serve and production AOT build complete without errors. This task creates the foundational project that all subsequent frontend tasks build upon.

## Dependent Tasks

- None (first task in the EP-TECH/us_001 sequence)

## Impacted Components

- New: Angular workspace root (`angular.json`, `tsconfig.json`, `package.json`)
- New: `src/app/` application root module and component
- New: `src/environments/` environment configuration files
- New: `.editorconfig`, `.gitignore` updates for Angular artifacts

## Implementation Plan

1. **Install Angular CLI 17.x globally** (or use npx) and generate a new Angular 17 workspace with `--strict` flag and SCSS as the default style preprocessor.
2. **Configure TypeScript strict mode** by verifying `tsconfig.json` has `strict: true`, `strictNullChecks`, `noImplicitAny`, `noImplicitReturns`, and `noFallthroughCasesInSwitch` enabled.
3. **Establish modular folder structure** under `src/app/` aligned with TR-001 layered architecture:
   - `core/` for singleton services, guards, interceptors
   - `shared/` for reusable components, directives, pipes
   - `features/` for lazy-loaded feature modules
   - `layouts/` for shell layout components
4. **Configure environment files** (`environment.ts` and `environment.prod.ts`) with API base URL placeholders and feature flags structure.
5. **Configure production build** in `angular.json` ensuring AOT compilation is enabled, budgets are set for initial bundle size (warn: 500kB, error: 1MB per NFR-001), and output hashing is configured.
6. **Validate development build** by running `ng serve` and confirming the app compiles and renders at `http://localhost:4200`.
7. **Validate production build** by running `ng build --configuration production` and confirming zero errors with AOT compilation.

## Current Project State

```text
propelIQ/
├── .github/
├── .propel/
├── .vscode/
├── BRD.md
├── README.md
└── .gitignore
```

> Placeholder: No existing Angular application. This task creates the initial project structure.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | app/angular.json | Angular workspace configuration with AOT and budget settings |
| CREATE | app/package.json | Node dependencies for Angular 17 |
| CREATE | app/tsconfig.json | Root TypeScript config with strict mode enabled |
| CREATE | app/tsconfig.app.json | Application-specific TypeScript config |
| CREATE | app/src/main.ts | Application bootstrap entry point |
| CREATE | app/src/app/app.component.ts | Root application component |
| CREATE | app/src/app/app.config.ts | Application configuration with providers |
| CREATE | app/src/app/app.routes.ts | Root route definitions (placeholder) |
| CREATE | app/src/app/core/ | Core module directory for guards, interceptors, services |
| CREATE | app/src/app/shared/ | Shared module directory for reusable components |
| CREATE | app/src/app/features/ | Features directory for lazy-loaded modules |
| CREATE | app/src/app/layouts/ | Layouts directory for shell components |
| CREATE | app/src/environments/environment.ts | Development environment config |
| CREATE | app/src/environments/environment.prod.ts | Production environment config |

## External References

- Angular CLI workspace setup (v17): https://github.com/angular/angular/blob/17.3.12/aio/content/guide/standalone-components.md
- Angular strict mode: https://angular.io/guide/strict-mode
- Angular workspace configuration: https://angular.io/guide/workspace-config
- Angular AOT compilation: https://angular.io/guide/aot-compiler
- Angular 17 standalone bootstrap: Uses `bootstrapApplication()` with `provideRouter()` for standalone-first architecture

## Build Commands

```bash
# Install dependencies
npm install

# Serve development build
ng serve

# Production build with AOT
ng build --configuration production
```

## Implementation Validation Strategy

- [ ] `npm install` completes without errors
- [ ] `ng serve` compiles and serves at `http://localhost:4200`
- [ ] `ng build --configuration production` completes with AOT and zero TypeScript errors
- [ ] TypeScript strict mode flags are all enabled in `tsconfig.json`
- [ ] Bundle budget thresholds are configured in `angular.json`
- [ ] Folder structure matches modular layered architecture (core/, shared/, features/, layouts/)

## Implementation Checklist

- [ ] Generate Angular 17 workspace using `ng new` with `--strict`, `--style=scss`, `--routing`, and standalone component defaults
- [ ] Verify `tsconfig.json` strict mode flags (`strict`, `noImplicitAny`, `noImplicitReturns`, `noFallthroughCasesInSwitch`)
- [ ] Create `src/app/core/`, `src/app/shared/`, `src/app/features/`, `src/app/layouts/` directories with barrel index files
- [ ] Configure `src/environments/environment.ts` and `src/environments/environment.prod.ts` with API base URL and feature flag placeholders
- [ ] Set production build budgets in `angular.json` (initial: warn 500kB / error 1MB) per NFR-001
- [ ] Run `ng serve` and confirm compilation success and rendering at localhost:4200
- [ ] Run `ng build --configuration production` and confirm zero errors with AOT

# Task - TASK_001

## Requirement Reference

- User Story: us_002
- Story Location: .propel/context/tasks/EP-TECH/us_002/us_002.md
- Acceptance Criteria:
  - AC-1: Given the repository is cloned, When `dotnet build` is executed, Then the solution compiles without errors and the Web API starts on the configured port.
  - AC-3: Given the modular architecture is in place, When a new bounded module (Scheduling, Clinical Intelligence, Administration, Shared Services) is scaffolded, Then it follows the layered pattern: Controllers -> Application Services -> Domain -> Data with no reverse dependencies.
- Edge Case:
  - N/A (solution scaffold task; edge cases addressed in task_002)

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
| Backend | ASP.NET Core Web API | 8.x |
| Database | N/A | N/A |
| Library | Entity Framework Core | 8.x |
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

Create the ASP.NET Core 8 Web API solution with a modular layered monolith structure following TR-001 bounded module conventions. The solution establishes four domain modules (Scheduling, ClinicalIntelligence, Administration, SharedServices) each with the layered pattern (Controllers/API -> Application -> Domain -> Infrastructure/Data). Project references enforce uni-directional dependency flow with no reverse references. The API host project serves as the composition root. This task produces the foundational backend structure that all subsequent backend feature tasks build upon.

## Dependent Tasks

- None (first backend task in the EP-TECH/us_002 sequence)

## Impacted Components

- New: `server/PropelIQ.sln` (solution file with all projects)
- New: `server/src/PropelIQ.Api/` (Web API host / composition root)
- New: `server/src/PropelIQ.SharedKernel/` (cross-cutting contracts, base classes)
- New: `server/src/Modules/Scheduling/` (Scheduling module with 4 layers)
- New: `server/src/Modules/ClinicalIntelligence/` (Clinical Intelligence module with 4 layers)
- New: `server/src/Modules/Administration/` (Administration module with 4 layers)
- New: `server/src/Modules/SharedServices/` (Shared Services module with 4 layers)

## Implementation Plan

1. **Create the .NET 8 solution** using `dotnet new sln` under a `server/` directory. Create the API host project using `dotnet new webapi --no-https false` targeting `net8.0`.
2. **Create the SharedKernel class library** (`PropelIQ.SharedKernel`) containing base entity classes, common interfaces (e.g., `IRepository<T>`), result types, and shared constants. All modules reference this project.
3. **Scaffold four bounded modules** each containing four layer projects following the naming convention:
   - `PropelIQ.Modules.<Module>.Api` - Controllers, request/response DTOs, route registration
   - `PropelIQ.Modules.<Module>.Application` - Application services, command/query handlers, validators
   - `PropelIQ.Modules.<Module>.Domain` - Domain entities, value objects, domain events, interfaces
   - `PropelIQ.Modules.<Module>.Infrastructure` - Data access (EF Core DbContext), external service clients, repository implementations
4. **Configure project references** enforcing uni-directional dependency flow:
   - `.Api` references `.Application`
   - `.Application` references `.Domain`
   - `.Infrastructure` references `.Domain` (implements interfaces)
   - `.Infrastructure` references `.Application` (for DI registration)
   - `.Domain` references `SharedKernel` only
   - No reverse dependencies allowed
5. **Configure the API host** (`PropelIQ.Api`) as the composition root that references all module `.Api` and `.Infrastructure` projects. Set up `Program.cs` with `WebApplication.CreateBuilder()`, controller registration, and endpoint routing.
6. **Add versioned API route prefix** `/api/v1` using route attribute conventions on controller base class aligned with TR-002.
7. **Verify the build** by running `dotnet build` and confirming zero errors across all projects.

### ASP.NET Core 8 Modular Project Reference Flow

```text
PropelIQ.Api (Composition Root)
  ├── references Modules.Scheduling.Api
  ├── references Modules.Scheduling.Infrastructure
  ├── references Modules.ClinicalIntelligence.Api
  ├── references Modules.ClinicalIntelligence.Infrastructure
  ├── references Modules.Administration.Api
  ├── references Modules.Administration.Infrastructure
  ├── references Modules.SharedServices.Api
  └── references Modules.SharedServices.Infrastructure

Modules.<Name>.Api
  └── references Modules.<Name>.Application

Modules.<Name>.Application
  └── references Modules.<Name>.Domain

Modules.<Name>.Infrastructure
  ├── references Modules.<Name>.Domain
  └── references Modules.<Name>.Application

Modules.<Name>.Domain
  └── references PropelIQ.SharedKernel
```

Source: ASP.NET Core 8 modular monolith pattern per TR-001 architecture decision

## Current Project State

```text
propelIQ/
├── .github/
├── .propel/
├── .vscode/
├── app/              (Angular SPA from US_001)
├── BRD.md
├── README.md
└── .gitignore
```

> Placeholder: No existing backend project. This task creates the server/ directory and solution structure.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/PropelIQ.sln | Solution file with all project references |
| CREATE | server/src/PropelIQ.Api/PropelIQ.Api.csproj | Web API host project targeting net8.0 |
| CREATE | server/src/PropelIQ.Api/Program.cs | Composition root with service registration and middleware pipeline |
| CREATE | server/src/PropelIQ.Api/Controllers/BaseApiController.cs | Base controller with `/api/v1` route prefix |
| CREATE | server/src/PropelIQ.SharedKernel/PropelIQ.SharedKernel.csproj | Shared kernel class library |
| CREATE | server/src/PropelIQ.SharedKernel/BaseEntity.cs | Base entity with Id, CreatedAt, UpdatedAt |
| CREATE | server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Api/ | Scheduling API layer project |
| CREATE | server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Application/ | Scheduling application layer project |
| CREATE | server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Domain/ | Scheduling domain layer project |
| CREATE | server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Infrastructure/ | Scheduling infrastructure layer project |
| CREATE | server/src/Modules/ClinicalIntelligence/PropelIQ.Modules.ClinicalIntelligence.Api/ | ClinicalIntelligence API layer project |
| CREATE | server/src/Modules/ClinicalIntelligence/PropelIQ.Modules.ClinicalIntelligence.Application/ | ClinicalIntelligence application layer project |
| CREATE | server/src/Modules/ClinicalIntelligence/PropelIQ.Modules.ClinicalIntelligence.Domain/ | ClinicalIntelligence domain layer project |
| CREATE | server/src/Modules/ClinicalIntelligence/PropelIQ.Modules.ClinicalIntelligence.Infrastructure/ | ClinicalIntelligence infrastructure layer project |
| CREATE | server/src/Modules/Administration/PropelIQ.Modules.Administration.Api/ | Administration API layer project |
| CREATE | server/src/Modules/Administration/PropelIQ.Modules.Administration.Application/ | Administration application layer project |
| CREATE | server/src/Modules/Administration/PropelIQ.Modules.Administration.Domain/ | Administration domain layer project |
| CREATE | server/src/Modules/Administration/PropelIQ.Modules.Administration.Infrastructure/ | Administration infrastructure layer project |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Api/ | SharedServices API layer project |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Application/ | SharedServices application layer project |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Domain/ | SharedServices domain layer project |
| CREATE | server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/ | SharedServices infrastructure layer project |

## External References

- ASP.NET Core 8 Web API fundamentals: https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0
- .NET 8 modular monolith architecture: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
- ASP.NET Core project structure (v8): https://github.com/dotnet/aspnetcore/blob/v8.0.21
- API versioning with route attributes: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-8.0
- Clean Architecture with ASP.NET Core: Controller -> Application -> Domain -> Infrastructure dependency flow

## Build Commands

```bash
# Create solution and build
dotnet build server/PropelIQ.sln

# Run API project
dotnet run --project server/src/PropelIQ.Api/PropelIQ.Api.csproj
```

## Implementation Validation Strategy

- [ ] `dotnet build server/PropelIQ.sln` compiles all projects with zero errors
- [ ] `dotnet run --project server/src/PropelIQ.Api/PropelIQ.Api.csproj` starts the API on configured port
- [ ] All four bounded modules have four layer projects each (Api, Application, Domain, Infrastructure)
- [ ] Project references follow uni-directional flow with no reverse dependencies
- [ ] `BaseApiController` applies `[Route("api/v1/[controller]")]` attribute (TR-002)
- [ ] SharedKernel is referenced only by Domain projects

## Implementation Checklist

- [x] Create `server/` directory and initialize solution with `dotnet new sln`
- [x] Create `PropelIQ.Api` Web API host project targeting `net8.0` with controllers
- [x] Create `PropelIQ.SharedKernel` class library with `BaseEntity` and shared interfaces
- [x] Scaffold four bounded modules (Scheduling, ClinicalIntelligence, Administration, SharedServices) each with Api/Application/Domain/Infrastructure layer projects
- [x] Configure project references enforcing uni-directional dependency flow (no reverse dependencies)
- [x] Create `BaseApiController` with `[Route("api/v1/[controller]")]` and `[ApiController]` attributes
- [x] Configure `Program.cs` composition root with controller registration and endpoint routing
- [x] Run `dotnet build` and confirm zero errors across all projects

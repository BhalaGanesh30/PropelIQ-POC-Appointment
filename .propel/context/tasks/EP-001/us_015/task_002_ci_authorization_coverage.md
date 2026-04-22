# Task - TASK_002

## Requirement Reference

- User Story: us_015
- Story Location: .propel/context/tasks/EP-001/us_015/us_015.md
- Acceptance Criteria:
  - AC-4: Given a JWT token contains a role claim, When the API validates the token, Then the role claim is extracted and authorization policies are applied before the request reaches the controller action.
- Edge Cases:
  - How does the system handle an API endpoint missing an authorization attribute? CI/CD pipeline includes an authorization coverage check that fails if any endpoint is unannotated.

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
| Library | xUnit | latest stable |
| Library | Microsoft.AspNetCore.Mvc.Testing | 8.x |
| Library | System.Reflection | (runtime) |
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

Create an xUnit integration test suite that uses reflection to scan all API controller actions at build/CI time and fail if any endpoint is missing an explicit `[Authorize]` or `[AllowAnonymous]` attribute. This prevents accidental exposure of unprotected endpoints (Edge Case 2). The test is integrated into the GitHub Actions CI pipeline so the build fails before merge if any controller action lacks an authorization annotation. Additionally, a secondary test validates that all named authorization policies referenced in `[Authorize(Policy = "...")]` attributes are actually registered in the DI container, preventing runtime 500 errors from misconfigured policy names.

## Dependent Tasks

- task_001_be_rbac_policies (requires authorization policies to be registered)
- US_001 tasks (requires project scaffold and test project)

## Impacted Components

- New: `server/tests/PropelIQ.Api.Tests/Authorization/AuthorizationCoverageTests.cs` (reflection-based endpoint scanning tests)
- Modify: `.github/workflows/ci.yml` (ensure test step covers authorization tests)

## Implementation Plan

1. **Create `AuthorizationCoverageTests`** that uses reflection to scan all controller types and their public action methods, verifying each has either `[Authorize]` or `[AllowAnonymous]` at the method or class level:

```csharp
// server/tests/PropelIQ.Api.Tests/Authorization/
//   AuthorizationCoverageTests.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

public class AuthorizationCoverageTests
{
    private static readonly Assembly ApiAssembly =
        typeof(Program).Assembly;

    [Fact]
    public void All_Controller_Actions_Must_Have_Authorization_Attribute()
    {
        var controllerTypes = ApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t)
                && !t.IsAbstract
                && t.IsPublic);

        var unannotatedEndpoints = new List<string>();

        foreach (var controller in controllerTypes)
        {
            var classHasAuthorize = controller
                .GetCustomAttribute<AuthorizeAttribute>() is not null;
            var classHasAllowAnonymous = controller
                .GetCustomAttribute<AllowAnonymousAttribute>() is not null;

            var actions = controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance
                | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName);

            foreach (var action in actions)
            {
                var methodHasAuthorize = action
                    .GetCustomAttribute<AuthorizeAttribute>() is not null;
                var methodHasAllowAnonymous = action
                    .GetCustomAttribute<AllowAnonymousAttribute>() is not null;

                bool isCovered = classHasAuthorize
                    || classHasAllowAnonymous
                    || methodHasAuthorize
                    || methodHasAllowAnonymous;

                if (!isCovered)
                {
                    unannotatedEndpoints.Add(
                        $"{controller.Name}.{action.Name}");
                }
            }
        }

        Assert.True(
            unannotatedEndpoints.Count == 0,
            $"The following endpoints are missing [Authorize] or "
            + $"[AllowAnonymous] attributes:\n"
            + string.Join("\n", unannotatedEndpoints));
    }
}
```

2. **Add a policy registration validation test** that verifies all policy names referenced in `[Authorize(Policy = "...")]` attributes are registered in the authorization options. This prevents runtime failures from typos in policy names:

```csharp
[Fact]
public void All_Referenced_Policies_Must_Be_Registered()
{
    var controllerTypes = ApiAssembly.GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t)
            && !t.IsAbstract);

    var referencedPolicies = new HashSet<string>();

    foreach (var controller in controllerTypes)
    {
        // Class-level policy
        var classAttr = controller
            .GetCustomAttribute<AuthorizeAttribute>();
        if (!string.IsNullOrEmpty(classAttr?.Policy))
            referencedPolicies.Add(classAttr.Policy);

        // Method-level policies
        var actions = controller.GetMethods(
            BindingFlags.Public | BindingFlags.Instance
            | BindingFlags.DeclaredOnly);
        foreach (var action in actions)
        {
            var methodAttr = action
                .GetCustomAttribute<AuthorizeAttribute>();
            if (!string.IsNullOrEmpty(methodAttr?.Policy))
                referencedPolicies.Add(methodAttr.Policy);
        }
    }

    // Resolve registered policies from DI
    using var host = new WebApplicationFactory<Program>()
        .CreateClient();
    using var scope = new WebApplicationFactory<Program>()
        .Services.CreateScope();
    var authOptions = scope.ServiceProvider
        .GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    var unregisteredPolicies = referencedPolicies
        .Where(p => authOptions.GetPolicy(p) is null)
        .ToList();

    Assert.True(
        unregisteredPolicies.Count == 0,
        $"The following authorization policies are referenced but "
        + $"not registered:\n"
        + string.Join("\n", unregisteredPolicies));
}
```

3. **Add a test verifying FallbackPolicy is configured** to require authenticated users:

```csharp
[Fact]
public void FallbackPolicy_Must_Require_Authenticated_User()
{
    using var factory = new WebApplicationFactory<Program>();
    using var scope = factory.Services.CreateScope();
    var authOptions = scope.ServiceProvider
        .GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    Assert.NotNull(authOptions.FallbackPolicy);

    var requirements = authOptions.FallbackPolicy!
        .Requirements.ToList();
    Assert.Contains(requirements,
        r => r is DenyAnonymousAuthorizationRequirement);
}
```

4. **Add a test for role-policy alignment** that verifies each named policy requires at least one role claim, ensuring no empty policies exist:

```csharp
[Fact]
public void Named_Policies_Must_Require_At_Least_One_Role()
{
    using var factory = new WebApplicationFactory<Program>();
    using var scope = factory.Services.CreateScope();
    var authOptions = scope.ServiceProvider
        .GetRequiredService<IOptions<AuthorizationOptions>>().Value;

    var namedPolicies = new[]
    {
        AuthorizationPolicies.PatientOnly,
        AuthorizationPolicies.StaffOnly,
        AuthorizationPolicies.AdminOnly,
        AuthorizationPolicies.StaffOrAdmin,
    };

    var emptyPolicies = new List<string>();

    foreach (var policyName in namedPolicies)
    {
        var policy = authOptions.GetPolicy(policyName);
        if (policy is null
            || !policy.Requirements.OfType<RolesAuthorizationRequirement>()
                .Any())
        {
            emptyPolicies.Add(policyName);
        }
    }

    Assert.True(
        emptyPolicies.Count == 0,
        $"The following policies have no role requirements:\n"
        + string.Join("\n", emptyPolicies));
}
```

5. **Ensure CI pipeline runs these tests** as part of the standard `dotnet test` step. The existing `.github/workflows/ci.yml` from US_001 should already run `dotnet test` on all test projects. Verify the authorization test project is included in the solution file and discoverable by the test runner. Add an explicit step comment if needed:

```yaml
# In .github/workflows/ci.yml — existing test step covers this
- name: Run tests
  run: dotnet test --configuration Release --no-build --verbosity normal
  # Includes AuthorizationCoverageTests which fail the build
  # if any endpoint is missing authorization attributes
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── src/
│   │   └── PropelIQ.Api/
│   │       ├── Program.cs                     (from US_001)
│   │       ├── Controllers/
│   │       │   └── AuthController.cs          (from US_014 task_001)
│   │       └── Authorization/
│   │           ├── Policies/
│   │           │   └── AuthorizationPolicies.cs  (from task_001)
│   │           ├── Requirements/
│   │           │   └── PatientResourceRequirement.cs (from task_001)
│   │           └── Handlers/                  (from task_001)
│   └── tests/
│       └── PropelIQ.Api.Tests/
│           └── Authorization/
├── .github/
│   └── workflows/
│       └── ci.yml                             (from US_001)
└── client/                                    (from US_001)
```

> Placeholder: Update on execution based on US_001, task_001 completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/tests/PropelIQ.Api.Tests/Authorization/AuthorizationCoverageTests.cs | Reflection-based tests: endpoint annotation coverage, policy registration validation, FallbackPolicy check, role-policy alignment |
| MODIFY | .github/workflows/ci.yml | Verify authorization test project is included in dotnet test step (may need explicit comment only) |

## External References

- ASP.NET Core authorization testing: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-8.0
- WebApplicationFactory for integration testing: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0
- Reflection for attribute scanning: https://learn.microsoft.com/en-us/dotnet/api/system.reflection.memberinfo.getcustomattribute
- xUnit test patterns: https://xunit.net/docs/getting-started/netcore/cmdline

## Build Commands

```bash
# Build and run tests
cd server
dotnet build
dotnet test --configuration Release --verbosity normal

# Run only authorization coverage tests
dotnet test --filter "FullyQualifiedName~AuthorizationCoverageTests"

# CI pipeline (GitHub Actions)
# Triggered automatically on push/PR via .github/workflows/ci.yml
```

## Implementation Validation Strategy

- [ ] `All_Controller_Actions_Must_Have_Authorization_Attribute` test passes with all current controllers annotated
- [ ] Adding a controller action without `[Authorize]` or `[AllowAnonymous]` causes test failure
- [ ] `All_Referenced_Policies_Must_Be_Registered` test passes with all current policy references
- [ ] Referencing a non-existent policy name causes test failure
- [ ] `FallbackPolicy_Must_Require_Authenticated_User` test passes confirming secure-by-default
- [ ] `Named_Policies_Must_Require_At_Least_One_Role` test passes for all role-based policies
- [ ] CI pipeline fails the build if any authorization coverage test fails

## Implementation Checklist

- [x] Create `AuthorizationCoverageTests` class with reflection-based endpoint scanning test that fails on unannotated actions
- [x] Add policy registration validation test comparing `[Authorize(Policy = "...")]` attribute values against registered policies
- [x] Add DefaultPolicy configuration test verifying `DenyAnonymousAuthorizationRequirement` is present (adapted from FallbackPolicy — FallbackPolicy was removed in task_001 to avoid blocking health endpoints; enforcement uses `MapControllers().RequireAuthorization()` which applies DefaultPolicy)
- [x] Add role-policy alignment test verifying each named role policy requires at least one `RolesAuthorizationRequirement`
- [x] Verified CI pipeline `dotnet test PropelIQ.sln` step in `.github/workflows/ci.yml` discovers and executes authorization coverage tests (project added to solution)

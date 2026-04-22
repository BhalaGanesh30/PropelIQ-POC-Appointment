using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Api.Authorization.Policies;
using Xunit;

namespace PropelIQ.Api.Tests.Authorization;

/// <summary>
/// Reflection-based CI guard tests that fail the build if any controller action
/// is missing explicit authorization coverage or if a policy name referenced in
/// an [Authorize] attribute is not registered in the DI container.
///
/// These tests enforce the edge case requirement from us_015:
/// "CI/CD pipeline includes an authorization coverage check that fails if any
/// endpoint is unannotated."
/// </summary>
public sealed class AuthorizationCoverageTests
{
    // ── Shared fixtures ───────────────────────────────────────────────────

    /// <summary>
    /// The compiled API assembly. All controller types are discovered from here
    /// to ensure the scan is always in sync with the current build.
    /// </summary>
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    /// <summary>
    /// Builds a minimal DI container containing only the authorization policies
    /// registered by <see cref="AuthorizationPolicies.AddAppAuthorizationPolicies"/>.
    /// Avoids starting a full web host so no database or Redis connection is needed.
    /// </summary>
    private static AuthorizationOptions BuildAuthorizationOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders()); // suppress test output noise
        services.AddAppAuthorizationPolicies();
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
    }

    /// <summary>
    /// Returns all concrete, public controller types from the API assembly —
    /// i.e., types that derive from <see cref="ControllerBase"/> but are not abstract.
    /// </summary>
    private static IEnumerable<Type> GetControllerTypes() =>
        ApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t)
                && !t.IsAbstract
                && t.IsPublic);

    // ── Test 1: Endpoint annotation coverage ─────────────────────────────

    /// <summary>
    /// Verifies that every public controller action has either <see cref="AuthorizeAttribute"/>
    /// or <see cref="AllowAnonymousAttribute"/> at the method level or the declaring class level.
    /// A class-level [Authorize] or [AllowAnonymous] covers all actions on that class.
    /// Methods decorated with [NonAction] are excluded as they are not HTTP endpoints.
    /// </summary>
    [Fact]
    public void All_Controller_Actions_Must_Have_Authorization_Attribute()
    {
        var unannotated = new List<string>();

        foreach (var controller in GetControllerTypes())
        {
            var classHasAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>() is not null;
            var classHasAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

            var actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName
                    && m.GetCustomAttribute<NonActionAttribute>() is null);

            foreach (var action in actions)
            {
                var methodHasAuthorize = action.GetCustomAttribute<AuthorizeAttribute>() is not null;
                var methodHasAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

                bool isCovered = classHasAuthorize
                    || classHasAnonymous
                    || methodHasAuthorize
                    || methodHasAnonymous;

                if (!isCovered)
                {
                    unannotated.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        Assert.True(
            unannotated.Count == 0,
            "The following controller actions are missing [Authorize] or [AllowAnonymous]:\n"
            + string.Join("\n", unannotated));
    }

    // ── Test 2: Policy name registration validation ───────────────────────

    /// <summary>
    /// Scans all controller actions for <c>[Authorize(Policy = "...")]</c> attributes
    /// and verifies that each referenced policy name is registered in the authorization
    /// options. Prevents runtime 500 errors caused by typos in policy name strings.
    /// </summary>
    [Fact]
    public void All_Referenced_Policies_Must_Be_Registered()
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var controller in GetControllerTypes())
        {
            AddPolicyName(controller.GetCustomAttribute<AuthorizeAttribute>(), referenced);

            foreach (var action in controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                AddPolicyName(action.GetCustomAttribute<AuthorizeAttribute>(), referenced);
            }
        }

        var options = BuildAuthorizationOptions();

        var unregistered = referenced
            .Where(p => options.GetPolicy(p) is null)
            .OrderBy(p => p)
            .ToList();

        Assert.True(
            unregistered.Count == 0,
            "The following [Authorize(Policy=\"...\")] names have no matching registered policy:\n"
            + string.Join("\n", unregistered));
    }

    private static void AddPolicyName(AuthorizeAttribute? attr, HashSet<string> target)
    {
        if (!string.IsNullOrWhiteSpace(attr?.Policy))
            target.Add(attr.Policy);
    }

    // ── Test 3: Default policy enforces authenticated user ────────────────

    /// <summary>
    /// Verifies that the authorization <c>DefaultPolicy</c> requires an authenticated user.
    /// The default policy is applied by <c>app.MapControllers().RequireAuthorization()</c>
    /// in Program.cs, which is the enforcement mechanism used instead of a FallbackPolicy
    /// (FallbackPolicy was intentionally removed to avoid blocking non-controller
    /// endpoints such as health checks and Prometheus metrics).
    /// </summary>
    [Fact]
    public void DefaultPolicy_Must_Require_Authenticated_User()
    {
        var options = BuildAuthorizationOptions();

        Assert.NotNull(options.DefaultPolicy);
        Assert.Contains(
            options.DefaultPolicy.Requirements,
            r => r is DenyAnonymousAuthorizationRequirement);
    }

    // ── Test 4: Role-based policies have role requirements ───────────────

    /// <summary>
    /// Verifies that each role-based named policy contains at least one
    /// <see cref="RolesAuthorizationRequirement"/> so that empty or misconfigured
    /// role policies cannot silently permit all authenticated users.
    /// The <c>PatientResourceOwner</c> policy is excluded because it uses a custom
    /// <c>PatientResourceRequirement</c> rather than a role claim.
    /// </summary>
    [Fact]
    public void Named_Role_Policies_Must_Require_At_Least_One_Role()
    {
        var options = BuildAuthorizationOptions();

        // PatientResourceOwner is intentionally excluded: it uses PatientResourceRequirement.
        var rolePolicies = new[]
        {
            AuthorizationPolicies.PatientOnly,
            AuthorizationPolicies.StaffOnly,
            AuthorizationPolicies.AdminOnly,
            AuthorizationPolicies.StaffOrAdmin,
        };

        var missing = rolePolicies
            .Where(name =>
            {
                var policy = options.GetPolicy(name);
                return policy is null
                    || !policy.Requirements.OfType<RolesAuthorizationRequirement>().Any();
            })
            .ToList();

        Assert.True(
            missing.Count == 0,
            "The following role-based policies are missing a RolesAuthorizationRequirement:\n"
            + string.Join("\n", missing));
    }
}

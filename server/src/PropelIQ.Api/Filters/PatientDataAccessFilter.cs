using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Api.Filters;

/// <summary>
/// ASP.NET Core action filter that intercepts every successful response from a controller
/// action that reads patient data and emits a <c>DataAccess</c> audit event via
/// <see cref="IAuditRecordService"/> (US_057, AC-1).
///
/// Apply to patient-data controllers via <c>[ServiceFilter(typeof(PatientDataAccessFilter))]</c>.
///
/// Captured dimensions:
/// - <c>actorUserId</c>: authenticated user's sub claim.
/// - <c>accessorRole</c>: JWT role claim (or "System" for automated processes — edge case 2).
/// - <c>patientId</c>: extracted from route/query ("patientId") or the "/patients/me" pattern.
/// - <c>resourceType</c>: controller name without suffix (e.g., "PatientProfile").
/// - <c>entityId</c>: route/query "id" argument when present.
/// - <c>httpMethod</c> + <c>path</c>: raw request details for access trails.
///
/// Only HTTP 2xx responses trigger the audit write; errors are not logged as accesses.
/// </summary>
public sealed class PatientDataAccessFilter : IAsyncActionFilter
{
    private readonly IAuditRecordService _audit;

    public PatientDataAccessFilter(IAuditRecordService audit)
    {
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var resultContext = await next();

        // Only emit for successful (2xx) read responses.
        var statusCode = resultContext.Result is ObjectResult obj
            ? obj.StatusCode
            : resultContext.HttpContext.Response.StatusCode;

        if (statusCode is null or < 200 or >= 300)
            return;

        var user = context.HttpContext.User;

        // OWASP A01: require an authenticated sub claim before writing.
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? user.FindFirstValue("sub");
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var actorUserId))
            return;

        // Edge case 2: service accounts use the "System" role claim.
        var role = user.FindFirstValue(ClaimTypes.Role)
                ?? user.FindFirstValue("role")
                ?? "Unknown";

        var patientId = ExtractPatientId(context, userIdStr);
        if (patientId is null)
            return;

        var resourceType = context.Controller.GetType().Name.Replace("Controller", "", StringComparison.Ordinal);
        var entityId     = ExtractEntityId(context);
        var path         = context.HttpContext.Request.Path.Value ?? string.Empty;
        var method       = context.HttpContext.Request.Method;

        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = actorUserId,
            EventType  = "DataAccess",
            EntityType = resourceType,
            EntityId   = entityId,
            Details    = new Dictionary<string, object>
            {
                ["patientId"]    = patientId.Value.ToString(),
                ["accessorRole"] = role,
                ["httpMethod"]   = method,
                ["path"]         = path,
            },
        }, context.HttpContext.RequestAborted);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static Guid? ExtractPatientId(
        ActionExecutingContext context,
        string actorUserIdStr)
    {
        // 1. Explicit "patientId" route or query argument (staff / admin access).
        if (context.ActionArguments.TryGetValue("patientId", out var pidArg)
            && pidArg is Guid explicitId)
            return explicitId;

        // 2. String form of patientId (query string binding lands as string in some controllers).
        if (context.ActionArguments.TryGetValue("patientId", out var pidStr)
            && pidStr is string pidString
            && Guid.TryParse(pidString, out var parsedPid))
            return parsedPid;

        // 3. "/patients/me" pattern — patient accessing their own data.
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (path.Contains("/patients/me", StringComparison.OrdinalIgnoreCase))
            return Guid.TryParse(actorUserIdStr, out var selfId) ? selfId : null;

        // 4. Route value "id" when the controller owns a patient-scoped route
        //    (e.g., /patients/{id}/profile).
        if (context.HttpContext.Request.RouteValues.TryGetValue("id", out var routeId)
            && routeId is string routeIdStr
            && Guid.TryParse(routeIdStr, out var routePatientId))
            return routePatientId;

        return null;
    }

    private static Guid? ExtractEntityId(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("id", out var id) && id is Guid entityId)
            return entityId;

        if (context.HttpContext.Request.RouteValues.TryGetValue("id", out var rv)
            && rv is string rvStr
            && Guid.TryParse(rvStr, out var routeEntityId))
            return routeEntityId;

        return null;
    }
}

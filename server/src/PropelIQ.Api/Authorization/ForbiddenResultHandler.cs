using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace PropelIQ.Api.Authorization;

/// <summary>
/// Replaces the default 403 response with a standardized RFC 9110
/// ProblemDetails body so all access-denied responses are structured JSON
/// consistent with the rest of the API error surface (AC-1, AC-3).
/// Unauthenticated (401) and success cases are delegated to the default handler.
/// </summary>
public sealed class ForbiddenResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You do not have permission to access this resource.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

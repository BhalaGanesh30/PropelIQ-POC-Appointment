using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace PropelIQ.Api.Authorization.Handlers;

/// <summary>
/// Cross-cutting authorization handler that records every access-control
/// decision to the structured log (NFR-010 audit trail, AC-1, AC-3).
///
/// Registered LAST in DI so other handlers have already updated the context
/// state before this handler inspects it.
///
/// Structured log fields: UserId, Role, Endpoint, IP, PendingRequirements.
/// These are automatically forwarded to the OTel pipeline (Loki / Grafana).
/// </summary>
public sealed class AuditAuthorizationHandler : IAuthorizationHandler
{
    private readonly ILogger<AuditAuthorizationHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditAuthorizationHandler(
        ILogger<AuditAuthorizationHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        var endpoint = httpContext?.GetEndpoint()?.DisplayName;
        var ip = httpContext?.Connection.RemoteIpAddress?.ToString();

        // Check denial first: explicit fail or unsatisfied requirements.
        if (context.HasFailed || context.PendingRequirements.Any())
        {
            var pending = string.Join(", ",
                context.PendingRequirements.Select(r => r.GetType().Name));

            _logger.LogWarning(
                "Authorization DENIED: UserId={UserId} Role={Role} " +
                "Endpoint={Endpoint} IP={IP} PendingRequirements={Requirements}",
                userId, role, endpoint, ip, pending);
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            // Only log GRANTED for authenticated users to suppress noise from
            // anonymous endpoints that succeed through [AllowAnonymous].
            _logger.LogInformation(
                "Authorization GRANTED: UserId={UserId} Role={Role} " +
                "Endpoint={Endpoint} IP={IP}",
                userId, role, endpoint, ip);
        }

        return Task.CompletedTask;
    }
}

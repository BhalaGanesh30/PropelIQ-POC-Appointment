using Microsoft.EntityFrameworkCore;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Api.Infrastructure.Tenancy;

/// <summary>
/// Sets the PostgreSQL session variable <c>app.current_tenant_id</c> from the
/// authenticated user's <c>tenant_id</c> JWT claim. RLS policies on all
/// tenant-bearing tables use this value to enforce row-level isolation.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;

        if (!string.IsNullOrEmpty(tenantId))
        {
            // Parameterized via FormattableString to prevent SQL injection.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SET app.current_tenant_id = {tenantId}");
        }

        await _next(context);
    }
}

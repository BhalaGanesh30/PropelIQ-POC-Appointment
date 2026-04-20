using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PropelIQ.Modules.Scheduling.Infrastructure;

/// <summary>
/// DI registration for the Scheduling module infrastructure layer.
/// Called from the API composition root (Program.cs) to register
/// EF Core DbContext, repository implementations, and external clients.
/// </summary>
public static class SchedulingServiceRegistration
{
    public static IServiceCollection AddSchedulingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO (EP-SCH): Register SchedulingDbContext, repository implementations,
        // and any external service clients required by the Scheduling bounded context.
        return services;
    }
}

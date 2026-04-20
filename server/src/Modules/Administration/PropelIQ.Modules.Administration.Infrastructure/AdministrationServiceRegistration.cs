using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PropelIQ.Modules.Administration.Infrastructure;

/// <summary>
/// DI registration for the Administration module infrastructure layer.
/// Called from the API composition root (Program.cs) to register
/// EF Core DbContext, repository implementations, and admin-specific services.
/// </summary>
public static class AdministrationServiceRegistration
{
    public static IServiceCollection AddAdministrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO (EP-ADM): Register AdministrationDbContext, repository implementations,
        // audit log writer, and role/permission store dependencies.
        return services;
    }
}

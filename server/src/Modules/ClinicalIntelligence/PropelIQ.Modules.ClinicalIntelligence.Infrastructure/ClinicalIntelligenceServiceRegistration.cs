using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure;

/// <summary>
/// DI registration for the ClinicalIntelligence module infrastructure layer.
/// Called from the API composition root (Program.cs) to register
/// EF Core DbContext, repository implementations, and AI gateway clients.
/// </summary>
public static class ClinicalIntelligenceServiceRegistration
{
    public static IServiceCollection AddClinicalIntelligenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO (EP-CI): Register ClinicalIntelligenceDbContext, repository implementations,
        // AI gateway client (TR-008), and OCR/extraction worker dependencies.
        return services;
    }
}

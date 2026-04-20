using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropelIQ.Modules.SharedServices.Infrastructure.Caching;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.SharedServices.Infrastructure;

/// <summary>
/// DI registration for the SharedServices module infrastructure layer.
/// Called from the API composition root (Program.cs) to register
/// cross-cutting infrastructure: email, storage, notification, and caching clients.
/// </summary>
public static class SharedServicesServiceRegistration
{
    public static IServiceCollection AddSharedServicesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register the shared AppDbContext with Npgsql + pgvector support.
        // DR-001: PostgreSQL 15 primary datastore; UseVector() enables vector(n) column mapping.
        // The connection string key "Default" matches appsettings.json and .env.example.
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing from configuration. " +
                "Ensure appsettings.Development.json or environment variables are configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.UseVector()));

        // ── Redis Cache Service (TR-004) ─────────────────────────────────────
        // Bind CacheSettings section; RedisCacheService resolves IDistributedCache
        // registered by AddStackExchangeRedisCache in the API composition root.
        services.Configure<CacheOptions>(
            configuration.GetSection(CacheOptions.SectionName));

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ICacheInvalidator, SlotCacheInvalidator>();

        return services;
    }
}

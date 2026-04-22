using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Identity;

/// <summary>
/// Design-time factory for <see cref="AuthDbContext"/>.
/// Enables EF Core CLI tooling to scaffold Identity migrations without starting the host.
///
/// Usage (from solution root D:\IQ\server):
///   dotnet ef migrations add AddIdentityTables
///     --startup-project src/PropelIQ.Api
///     --project src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure
///     --context AuthDbContext
///     --output-dir Identity/Migrations
///     --configuration Release
/// </summary>
public sealed class AuthDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsHistoryTable("__auth_migrations_history", "auth"))
            .UseSnakeCaseNamingConvention();

        return new AuthDbContext(optionsBuilder.Options);
    }
}

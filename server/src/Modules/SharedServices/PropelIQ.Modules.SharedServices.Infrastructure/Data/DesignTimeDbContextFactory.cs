using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PropelIQ.Modules.SharedServices.Infrastructure.Data.Seed;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Data;

/// <summary>
/// Design-time factory for <see cref="AppDbContext"/>.
/// Required by the EF Core CLI tooling (<c>dotnet ef migrations add</c>) so it can
/// instantiate the DbContext without starting the full ASP.NET Core host.
///
/// The factory resolves the connection string from the API project's
/// appsettings.Development.json. The relative path assumes the CLI is invoked from
/// the solution root (<c>D:\IQ\server</c>) with:
///   <c>--startup-project src/PropelIQ.Api</c>
///   <c>--project src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure</c>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // The EF CLI sets working directory to the --startup-project folder (PropelIQ.Api).
        // Directory.GetCurrentDirectory() therefore already points to the Api project.
        var apiProjectPath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured in appsettings.Development.json. " +
                "Copy .env.example to .env and start the Docker PostgreSQL container.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", "app");
            })
            .UseSnakeCaseNamingConvention()
            .UseAsyncSeeding(async (context, _, ct) =>
                await AppDbContextSeed.SeedAsync((AppDbContext)context, ct));

        return new AppDbContext(optionsBuilder.Options);
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PropelIQ.Api.Infrastructure.HealthChecks;

/// <summary>
/// Database connectivity health check.
/// Returns <see cref="HealthStatus.Degraded"/> when the database is unreachable
/// so the API can start in reduced mode and retry on interval (US_002 Edge Case).
/// The connection string is read from configuration at check time — not at startup —
/// to allow the application to start even when the DB is temporarily unavailable.
/// Replace the stub ping with a real EF Core DbContext probe in the database task.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IConfiguration configuration, ILogger<DatabaseHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Stub: checks that a connection string is configured.
        // Replace with an actual DbConnection.OpenAsync() probe in the EF Core task.
        var connectionString = _configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Database health check: connection string 'Default' is not configured.");
            return Task.FromResult(
                HealthCheckResult.Degraded("Database connection string is not configured."));
        }

        // Placeholder — real connectivity test will be added when EF Core is wired.
        return Task.FromResult(HealthCheckResult.Healthy("Database connection string is configured."));
    }
}

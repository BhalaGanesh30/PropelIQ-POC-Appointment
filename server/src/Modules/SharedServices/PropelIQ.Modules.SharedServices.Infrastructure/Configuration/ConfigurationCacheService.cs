using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Configuration;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Configuration;

/// <summary>
/// Singleton in-memory cache for all configuration categories with write-through update semantics
/// (US_059, edge case 2).
///
/// <para>
/// Implements <see cref="IHostedService"/> to populate the cache on application startup by reading
/// the latest version for each <see cref="ConfigurationCategory"/> from the database.
/// Consumers (<c>ReminderWorker</c>, session middleware, slot service) call <see cref="TryGet"/>
/// for zero-latency reads. After every successful <see cref="IConfigurationService.UpdateAsync"/>
/// or <see cref="IConfigurationService.RestoreVersionAsync"/>, <see cref="UpdateCache"/> is called
/// to keep the in-process dictionary consistent.
/// </para>
///
/// <para>
/// Edge case 2 — in-flight operations: callers capture the <c>VersionId</c> at operation creation
/// time. They reference their captured version ID, not the live cache, so configuration changes
/// apply only to new operations from that point forward.
/// </para>
/// </summary>
public sealed class ConfigurationCacheService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigurationCacheService> _logger;

    private readonly ConcurrentDictionary<ConfigurationCategory, ConfigurationSnapshot> _cache = new();

    public ConfigurationCacheService(
        IServiceScopeFactory scopeFactory,
        ILogger<ConfigurationCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Populates the cache on application startup by loading the latest version per category.
    /// Failures are logged as warnings and do not prevent the host from starting — the service
    /// will fall back to database queries until the cache is populated after the first write.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var category in Enum.GetValues<ConfigurationCategory>())
            {
                var entity = await db.ConfigurationVersions
                    .Where(v => v.Category == category.ToString())
                    .OrderByDescending(v => v.VersionNumber)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);

                if (entity is null)
                    continue;

                var snapshot = new ConfigurationSnapshot
                {
                    VersionId     = entity.Id,
                    VersionNumber = entity.VersionNumber,
                    Category      = category,
                    Values        = DeserializeValues(entity.ValuesJson),
                    UpdatedAtUtc  = entity.ChangedAtUtc,
                    UpdatedByName = entity.ChangedByName
                };

                _cache[category] = snapshot;

                _logger.LogDebug(
                    "Configuration cache populated: {Category} v{Version}",
                    category, entity.VersionNumber);
            }

            _logger.LogInformation(
                "Configuration cache startup complete — {Count} categories loaded.",
                _cache.Count);
        }
        catch (Exception ex)
        {
            // Table may not exist until task_002 migration runs; allow host to start.
            _logger.LogWarning(ex,
                "Configuration cache could not be populated on startup. " +
                "Service will fall back to database queries.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Attempts to retrieve the cached snapshot for <paramref name="category"/>.</summary>
    public bool TryGet(ConfigurationCategory category, out ConfigurationSnapshot? snapshot) =>
        _cache.TryGetValue(category, out snapshot);

    /// <summary>
    /// Overwrites the cached snapshot for <paramref name="category"/>.
    /// Called by <see cref="ConfigurationService"/> after every successful write.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    public void UpdateCache(ConfigurationCategory category, ConfigurationSnapshot snapshot) =>
        _cache[category] = snapshot;

    private static Dictionary<string, object> DeserializeValues(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

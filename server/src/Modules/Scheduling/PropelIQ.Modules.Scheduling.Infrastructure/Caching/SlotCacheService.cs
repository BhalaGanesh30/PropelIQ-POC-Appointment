using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Scheduling;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Caching;

/// <summary>
/// Redis cache-aside wrapper for slot search results (TR-004).
/// Cache failures are non-fatal: logged as warnings and the request
/// falls through to the database (edge case: cache miss).
/// </summary>
public sealed class SlotCacheService
{
    private const int CacheTtlMinutes = 5;
    private const string CacheKeyPrefix = "slots";

    private readonly IDistributedCache _cache;
    private readonly ILogger<SlotCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SlotCacheService(IDistributedCache cache, ILogger<SlotCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Builds a deterministic composite cache key from the search parameters.
    /// Key format: slots:{dateFrom}:{dateTo}:{duration}:{type}
    /// </summary>
    public string BuildCacheKey(SlotSearchQuery query)
    {
        var dateFrom = query.DateFrom.ToString("yyyyMMdd");
        var dateTo = query.DateTo.ToString("yyyyMMdd");
        var duration = query.Duration?.ToString() ?? "any";
        var type = query.Type?.ToString() ?? "any";
        return $"{CacheKeyPrefix}:{dateFrom}:{dateTo}:{duration}:{type}";
    }

    public async Task<List<AppointmentSlot>?> GetAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is null) return null;

            return JsonSerializer.Deserialize<List<AppointmentSlot>>(cached, JsonOptions);
        }
        catch (Exception ex)
        {
            // Cache failure must not break the search flow — fall through to DB.
            _logger.LogWarning(ex, "Redis cache read failed for key {CacheKey}", cacheKey);
            return null;
        }
    }

    public async Task SetAsync(string cacheKey, List<AppointmentSlot> slots, CancellationToken ct)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(slots, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheTtlMinutes)
            };
            await _cache.SetStringAsync(cacheKey, serialized, options, ct);
        }
        catch (Exception ex)
        {
            // Cache write failure is non-critical — log and continue.
            _logger.LogWarning(ex, "Redis cache write failed for key {CacheKey}", cacheKey);
        }
    }
}

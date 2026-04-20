using Microsoft.Extensions.Logging;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Caching;

/// <summary>
/// Scheduling-domain implementation of <see cref="ICacheInvalidator"/>.
/// Removes slot-specific cache entries and related slot-search results when
/// a booking is confirmed, preventing stale availability from being served.
/// Full prefix invalidation (RemoveByPrefixAsync) is wired in EP-002.
/// </summary>
public sealed class SlotCacheInvalidator : ICacheInvalidator
{
    private readonly ICacheService _cache;
    private readonly ILogger<SlotCacheInvalidator> _logger;

    public SlotCacheInvalidator(ICacheService cache, ILogger<SlotCacheInvalidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvalidateSlotCacheAsync(Guid slotId, CancellationToken ct = default)
    {
        // Remove the exact slot entry (e.g. Scheduling:Slot:{slotId}).
        var slotKey = CacheKeyBuilder.Build("Scheduling", "Slot", slotId.ToString());
        await _cache.RemoveAsync(slotKey, ct);

        // Remove all slot-search result sets that may contain this slot.
        // RemoveByPrefixAsync is a no-op until EP-002 wires IConnectionMultiplexer.
        var searchPrefix = CacheKeyBuilder.BuildPrefix("Scheduling", "SlotSearch");
        await _cache.RemoveByPrefixAsync(searchPrefix, ct);

        _logger.LogInformation(
            "Cache invalidated for slot {SlotId}. Search prefix {Prefix} marked for eviction.",
            slotId, searchPrefix);
    }
}

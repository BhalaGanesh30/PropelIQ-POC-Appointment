namespace PropelIQ.SharedKernel.Caching;

/// <summary>
/// Cache invalidation contract for domain-specific key removal.
/// Implemented per module; wired into booking confirmation flows in EP-002.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Removes the cache entry for a specific slot and related slot-search result sets.
    /// Called on booking confirmation to prevent stale availability data.
    /// </summary>
    Task InvalidateSlotCacheAsync(Guid slotId, CancellationToken ct = default);
}

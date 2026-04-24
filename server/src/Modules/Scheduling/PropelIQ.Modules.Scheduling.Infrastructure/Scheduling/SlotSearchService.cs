using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Scheduling;
using PropelIQ.Modules.Scheduling.Infrastructure.Caching;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Scheduling;

/// <summary>
/// Implements cache-first slot search orchestration (AC-1, TR-004, NFR-002).
/// Lives in Infrastructure so it can access both the EF repository and Redis cache service.
/// </summary>
public sealed class SlotSearchService : ISlotSearchService
{
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.SlotSearch");

    private readonly ISlotRepository _slotRepo;
    private readonly SlotCacheService _cacheService;
    private readonly ILogger<SlotSearchService> _logger;

    public SlotSearchService(
        ISlotRepository slotRepo,
        SlotCacheService cacheService,
        ILogger<SlotSearchService> logger)
    {
        _slotRepo = slotRepo;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<SlotSearchResponse> SearchAsync(SlotSearchQuery query, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("SlotSearch");
        activity?.SetTag("search.date_from", query.DateFrom.ToString("O"));
        activity?.SetTag("search.date_to", query.DateTo.ToString("O"));
        activity?.SetTag("search.duration", query.Duration?.ToString());
        activity?.SetTag("search.type", query.Type?.ToString());

        // ── Cache-first pattern (AC-1, TR-004) ───────────────────────────────
        var cacheKey = _cacheService.BuildCacheKey(query);
        var cachedSlots = await _cacheService.GetAsync(cacheKey, ct);

        List<PropelIQ.Modules.Scheduling.Domain.Entities.AppointmentSlot> slots;

        if (cachedSlots is not null)
        {
            activity?.SetTag("search.cache_hit", true);
            // Re-filter for real-time freshness — a slot may have been booked since caching.
            var now = DateTimeOffset.UtcNow;
            slots = cachedSlots
                .Where(s => s.StartTime > now && s.CurrentBookings < s.MaxCapacity)
                .ToList();
        }
        else
        {
            activity?.SetTag("search.cache_hit", false);

            // Database fallback (edge case: cache miss) — repopulate with bounded TTL.
            slots = await _slotRepo.SearchAvailableSlotsAsync(
                query.DateFrom,
                query.DateTo,
                query.Duration,
                query.Type,
                ct);

            await _cacheService.SetAsync(cacheKey, slots, ct);
        }

        activity?.SetTag("search.result_count", slots.Count);
        _logger.LogDebug("Slot search returned {Count} slots (cache_hit={CacheHit})",
            slots.Count, cachedSlots is not null);

        // Group by date for frontend consumption.
        var grouped = slots
            .GroupBy(s => DateOnly.FromDateTime(s.StartTime.LocalDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new SlotGroupDto
            {
                Date = g.Key,
                Slots = g.OrderBy(s => s.StartTime)
                    .Select(s => new SlotDto
                    {
                        Id = s.Id,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        DurationMinutes = (int)s.Duration,
                        Type = s.Type.ToString(),
                        ProviderName = s.ProviderName,
                        Location = s.Location,
                        AvailableCapacity = s.MaxCapacity - s.CurrentBookings
                    })
                    .ToList()
            })
            .ToList();

        return new SlotSearchResponse
        {
            Days = grouped,
            TotalAvailableSlots = slots.Count
        };
    }
}

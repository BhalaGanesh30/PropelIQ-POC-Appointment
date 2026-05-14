using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Queue;
using PropelIQ.Modules.Scheduling.Application.Queue.Dto;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Queue;

/// <summary>
/// Implements <see cref="IQueueService"/> for the real-time queue dashboard
/// (EP-004 US_031).
///
/// AC-1: Returns today's appointments with queue-state, patient name, appointment
///       type, wait-time estimate, and isOverdue flag sorted by arrival time.
/// AC-2: Optional status filter is applied as a database predicate.
/// AC-3: IsOverdue driven by <see cref="IWaitTimeEstimationService"/>.
/// Edge Case 1: Redis miss falls through to PostgreSQL; cache is refreshed on miss.
///              Cache failures (network blip) are logged as warnings and never
///              surfaced to the caller.
/// NFR-002: 15-second Redis TTL (configurable via Queue:CacheTtlSeconds) keeps
///          p95 latency well under 500ms after the first request.
/// </summary>
public sealed class QueueService : IQueueService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly IWaitTimeEstimationService _waitTimeService;
    private readonly QueueOptions _options;
    private readonly ILogger<QueueService> _logger;

    public QueueService(
        AppDbContext db,
        IDistributedCache cache,
        IWaitTimeEstimationService waitTimeService,
        IOptions<QueueOptions> options,
        ILogger<QueueService> logger)
    {
        _db              = db;
        _cache           = cache;
        _waitTimeService = waitTimeService;
        _options         = options.Value;
        _logger          = logger;
    }

    /// <inheritdoc />
    public async Task<QueueResponseDto> GetTodayQueueAsync(
        QueueState? statusFilter,
        CancellationToken ct)
    {
        var cacheKey = BuildCacheKey(statusFilter);

        // ── 1. Cache-aside read (Edge Case 1) ─────────────────────────────────
        var cached = await TryGetFromCacheAsync(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Queue cache hit for key {CacheKey}", cacheKey);
            return cached;
        }

        // ── 2. Database query ─────────────────────────────────────────────────
        // Npgsql requires UTC DateTimeOffset values for timestamptz parameters.
        // Build explicit UTC boundaries (offset 00:00) to avoid local-offset writes.
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrow = today.AddDays(1);

        // Build a single query that joins Patients for PatientName.
        // AsNoTracking: read-only projection — no change tracking needed.
        var query =
            from appt in _db.Appointments.AsNoTracking()
            join patient in _db.Patients.AsNoTracking()
                on appt.PatientId equals patient.Id
            where appt.ScheduledAt >= today && appt.ScheduledAt < tomorrow
               && appt.Status != "Cancelled"
            select new { appt, patient };

        // AC-2: apply optional status filter.
        if (statusFilter.HasValue)
        {
            var statusString = statusFilter.Value.ToString();
            query = query.Where(x => x.appt.QueueState == statusString);
        }

        var rows = await query
            .OrderBy(x => x.appt.ScheduledAt)
            .ToListAsync(ct);

        // ── 3. Project into DTOs — single O(n) pass with index for queue position ──
        var now = DateTimeOffset.UtcNow;

        // Edge Case 2: using Select with index keeps overall complexity O(n).
        // IWaitTimeEstimationService.CalculateEstimatedWaitMinutes is called once
        // per row using the LINQ-provided index — no nested iteration over rows.
        var entries = rows
            .Select((row, index) =>
            {
                // ArrivedAt: not yet persisted until task_004 migration + check-in flow.
                DateTimeOffset? arrivedAt = null;
                var referenceTime = arrivedAt ?? row.appt.ScheduledAt;

                // O(n) contract: index is the 0-based queue position for this row.
                var estimatedWait = _waitTimeService.CalculateEstimatedWaitMinutes(
                    index,
                    row.appt.AppointmentType);

                var actualWait = (int)Math.Max(0, (now - referenceTime).TotalMinutes);

                // AC-3: IsOverdue uses arrivedAt (null until task_004 check-in flow).
                var isOverdue = _waitTimeService.IsOverdue(arrivedAt, estimatedWait);

                // Parse QueueState string stored on Appointment; default to Waiting.
                var status = Enum.TryParse<QueueState>(row.appt.QueueState, out var parsed)
                    ? parsed
                    : QueueState.Waiting;

                return new QueueEntryDto
                {
                    AppointmentId        = row.appt.Id,
                    PatientId            = row.appt.PatientId,
                    PatientName          = $"{row.patient.FirstName} {row.patient.LastName}",
                    AppointmentType      = row.appt.AppointmentType,
                    Status               = status,
                    ArrivedAt            = arrivedAt,
                    ScheduledAt          = row.appt.ScheduledAt,
                    EstimatedWaitMinutes = estimatedWait,
                    ActualWaitMinutes    = actualWait,
                    IsOverdue            = isOverdue,
                    // AC-3 (US_033): flag walk-in entries for dashboard badge.
                    IsWalkIn             = row.appt.AppointmentType == AppointmentType.WalkIn.ToString(),
                };
            })
            .OrderBy(e => e.ArrivedAt ?? e.ScheduledAt)
            .ToList();

        var response = new QueueResponseDto
        {
            Entries      = entries,
            TotalCount   = entries.Count,
            GeneratedAt  = now,
        };

        // ── 4. Populate cache on miss (Edge Case 1) ───────────────────────────
        await TrySetCacheAsync(cacheKey, response, ct);

        return response;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string BuildCacheKey(QueueState? statusFilter)
    {
        var date = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var status = statusFilter?.ToString() ?? "ALL";
        return $"queue:today:{date}:{status}";
    }

    private async Task<QueueResponseDto?> TryGetFromCacheAsync(string key, CancellationToken ct)
    {
        try
        {
            var json = await _cache.GetStringAsync(key, ct);
            if (json is null) return null;
            return JsonSerializer.Deserialize<QueueResponseDto>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // Edge Case 1: cache failures are non-fatal.
            _logger.LogWarning(ex, "Redis read failed for queue cache key {CacheKey}", key);
            return null;
        }
    }

    private async Task TrySetCacheAsync(string key, QueueResponseDto response, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(response, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CacheTtlSeconds),
            };
            await _cache.SetStringAsync(key, json, options, ct);
        }
        catch (Exception ex)
        {
            // Edge Case 1: cache write failure is non-critical.
            _logger.LogWarning(ex, "Redis write failed for queue cache key {CacheKey}", key);
        }
    }
}

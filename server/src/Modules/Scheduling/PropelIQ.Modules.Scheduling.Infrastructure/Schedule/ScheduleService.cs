using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Schedule.Dto;
using PropelIQ.Modules.Scheduling.Domain.Enums;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Schedule;

/// <summary>
/// Implements <see cref="IScheduleService"/> for EP-004 US_036 (FR-SO-006).
///
/// AC-1: Date-filtered EF Core query joined with Patients for display names.
/// AC-2: Reschedule validates conflicts, updates ScheduledAt within a transaction,
///       and writes an immutable <c>ScheduleReschedule</c> audit record.
/// AC-4: 30-second Redis TTL keeps p95 latency under 500ms after warm-up.
/// Edge Case 1: <see cref="ScheduleConflictException"/> thrown when target slot is occupied.
/// Edge Case 2: Empty entries list returned for dates with no appointments.
/// NFR-010: OTel span emitted per call.
/// </summary>
public sealed class ScheduleService : IScheduleService
{
    // ── OTel ───────────────────────────────────────────────────────────────────
    private static readonly ActivitySource ActivitySource =
        new("PropelIQ.Scheduling.ScheduleService");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Cache TTL — 30 seconds to meet NFR-002 (500ms p95) with acceptable staleness.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly IAuditService _auditService;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        AppDbContext db,
        IDistributedCache cache,
        IAuditService auditService,
        ILogger<ScheduleService> logger)
    {
        _db           = db;
        _cache        = cache;
        _auditService = auditService;
        _logger       = logger;
    }

    // ── GetDailyScheduleAsync (AC-1, AC-4) ────────────────────────────────────

    /// <inheritdoc />
    public async Task<DailyScheduleResponseDto> GetDailyScheduleAsync(
        DateOnly date,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("schedule.get_daily");
        activity?.SetTag("schedule.date", date.ToString("yyyy-MM-dd"));

        var cacheKey = BuildScheduleCacheKey(date);

        // ── 1. Cache-aside read (AC-4) ─────────────────────────────────────────
        var cached = await TryGetScheduleFromCacheAsync(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Schedule cache hit for date {Date}", date);
            return cached;
        }

        // ── 2. Database query (AC-1) ───────────────────────────────────────────
        // Window: start of day UTC to start of next day UTC for the given DateOnly.
        var dayStart  = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd    = dayStart.AddDays(1);

        var rows = await (
            from appt in _db.Appointments.AsNoTracking()
            join patient in _db.Patients.AsNoTracking()
                on appt.PatientId equals patient.Id
            where appt.ScheduledAt >= dayStart
               && appt.ScheduledAt < dayEnd
               && appt.Status != AppointmentStatus.Cancelled.ToString()
            orderby appt.ScheduledAt
            select new
            {
                appt.Id,
                appt.AppointmentType,
                appt.ScheduledAt,
                appt.DurationMinutes,
                appt.Status,
                appt.StaffUserId,
                appt.Location,
                PatientFirstName = patient.FirstName,
                PatientLastName  = patient.LastName,
            }
        ).ToListAsync(ct);

        var entries = rows
            .Select(r => new DailyScheduleEntryDto
            {
                AppointmentId   = r.Id,
                PatientName     = $"{r.PatientFirstName} {r.PatientLastName}",
                AppointmentType = r.AppointmentType,
                StartTime       = r.ScheduledAt,
                DurationMinutes = r.DurationMinutes,
                Status          = r.Status,
                Location        = r.Location,
            })
            .ToList();

        var response = new DailyScheduleResponseDto
        {
            Date       = date,
            Entries    = entries,
            TotalCount = entries.Count,
        };

        // ── 3. Populate cache (AC-4) ──────────────────────────────────────────
        await TrySetScheduleCacheAsync(cacheKey, response, ct);

        return response;
    }

    // ── RescheduleAsync (AC-2, Edge Case 1) ───────────────────────────────────

    /// <inheritdoc />
    public async Task<RescheduleResponseDto> RescheduleAsync(
        RescheduleRequestDto request,
        Guid staffUserId,
        CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("schedule.reschedule");
        activity?.SetTag("appointment.id", request.AppointmentId.ToString());
        activity?.SetTag("new_start_time",  request.NewStartTime.ToString("O"));

        // ── 1. Load the appointment ────────────────────────────────────────────
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct)
            ?? throw new KeyNotFoundException(
                $"Appointment {request.AppointmentId} was not found.");

        var oldStartTime = appointment.ScheduledAt;
        var newStart     = request.NewStartTime.ToUniversalTime();
        var newEnd       = newStart.AddMinutes(appointment.DurationMinutes);

        // ── 2. Conflict detection (Edge Case 1) ───────────────────────────────
        // Find any confirmed appointment for the same day whose time range overlaps
        // [newStart, newEnd). Excludes the appointment being rescheduled.
        var conflictRow = await (
            from other in _db.Appointments.AsNoTracking()
            join patient in _db.Patients.AsNoTracking()
                on other.PatientId equals patient.Id
            where other.Id != request.AppointmentId
               && other.Status != AppointmentStatus.Cancelled.ToString()
               && other.ScheduledAt < newEnd
               && other.ScheduledAt.AddMinutes(other.DurationMinutes) > newStart
            select new
            {
                other.Id,
                other.AppointmentType,
                other.ScheduledAt,
                other.DurationMinutes,
                other.Status,
                other.Location,
                PatientFirstName = patient.FirstName,
                PatientLastName  = patient.LastName,
            }
        ).FirstOrDefaultAsync(ct);

        if (conflictRow is not null)
        {
            var conflictEntry = new DailyScheduleEntryDto
            {
                AppointmentId   = conflictRow.Id,
                PatientName     = $"{conflictRow.PatientFirstName} {conflictRow.PatientLastName}",
                AppointmentType = conflictRow.AppointmentType,
                StartTime       = conflictRow.ScheduledAt,
                DurationMinutes = conflictRow.DurationMinutes,
                Status          = conflictRow.Status,
                Location        = conflictRow.Location,
            };

            throw new ScheduleConflictException(conflictEntry);
        }

        // ── 3. Audit log entry — added to change tracker before SaveChanges ───
        // Reuses LogOverrideAsync with ConstraintType="ScheduleReschedule" to
        // produce EventType="Override" with reschedule metadata (NFR-010, DR-002).
        var auditPayload = new OverrideAuditPayload
        {
            AppointmentId    = appointment.Id,
            ConstraintType   = "ScheduleReschedule",
            Reason           = request.OverrideReason,
            Action           = $"Reschedule from {oldStartTime:O} to {newStart:O}",
            // No pre-existing override record; use Guid.Empty sentinel — the audit
            // record being created here IS the override record.
            OverrideRecordId = Guid.Empty,
        };

        var auditRecordId = await _auditService.LogOverrideAsync(
            auditPayload, staffUserId, ct);

        // ── 4. Apply reschedule within a transaction (AC-2, DR-002) ───────────
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            appointment.ScheduledAt = newStart;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        // ── 5. Invalidate cache for affected date(s) ──────────────────────────
        var oldDate = DateOnly.FromDateTime(oldStartTime.UtcDateTime);
        var newDate = DateOnly.FromDateTime(newStart.UtcDateTime);

        await InvalidateScheduleCacheAsync(oldDate, ct);
        if (newDate != oldDate)
            await InvalidateScheduleCacheAsync(newDate, ct);

        // Also invalidate the queue cache for both dates (queue:today:{date}:*).
        await InvalidateQueueCacheAsync(oldDate, ct);
        if (newDate != oldDate)
            await InvalidateQueueCacheAsync(newDate, ct);

        return new RescheduleResponseDto
        {
            AppointmentId = appointment.Id,
            OldStartTime  = oldStartTime,
            NewStartTime  = newStart,
            AuditRecordId = auditRecordId,
        };
    }

    // ── Private cache helpers ──────────────────────────────────────────────────

    private static string BuildScheduleCacheKey(DateOnly date) =>
        $"schedule:daily:{date:yyyy-MM-dd}";

    private async Task<DailyScheduleResponseDto?> TryGetScheduleFromCacheAsync(
        string key, CancellationToken ct)
    {
        try
        {
            var json = await _cache.GetStringAsync(key, ct);
            if (json is null) return null;
            return JsonSerializer.Deserialize<DailyScheduleResponseDto>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for schedule cache key {CacheKey}", key);
            return null;
        }
    }

    private async Task TrySetScheduleCacheAsync(
        string key, DailyScheduleResponseDto response, CancellationToken ct)
    {
        try
        {
            var json    = JsonSerializer.Serialize(response, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
            };
            await _cache.SetStringAsync(key, json, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for schedule cache key {CacheKey}", key);
        }
    }

    private async Task InvalidateScheduleCacheAsync(DateOnly date, CancellationToken ct)
    {
        try
        {
            await _cache.RemoveAsync(BuildScheduleCacheKey(date), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate schedule cache for date {Date}", date);
        }
    }

    private async Task InvalidateQueueCacheAsync(DateOnly date, CancellationToken ct)
    {
        // Invalidate all queue state variants for the affected date.
        var dateStr    = date.ToString("yyyyMMdd");
        var allStates  = Enum.GetNames<QueueState>().Append("ALL");

        foreach (var state in allStates)
        {
            try
            {
                await _cache.RemoveAsync($"queue:today:{dateStr}:{state}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to invalidate queue cache key queue:today:{Date}:{State}",
                    dateStr, state);
            }
        }
    }
}

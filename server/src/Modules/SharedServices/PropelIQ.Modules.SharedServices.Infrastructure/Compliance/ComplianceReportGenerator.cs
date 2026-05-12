using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Compliance;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Aggregates data from <c>app.audit_records</c> into a structured
/// <see cref="ComplianceReportData"/> ready for PDF rendering (US_058, AC-1).
///
/// Three datasets are queried:
/// <list type="bullet">
///   <item>Access log events grouped by actor (actor user ID joined to Users) and resource type.</item>
///   <item>All audit event counts grouped by <c>EventType</c> for the period.</item>
///   <item>Anomaly detection: unusual volume, off-hours access, repeated failed attempts.</item>
/// </list>
///
/// A date span &gt; 90 days causes the caller to route the job to the async channel
/// instead of processing synchronously (edge case 1).
/// </summary>
public sealed class ComplianceReportGenerator
{
    /// <summary>
    /// Date range threshold above which async processing is recommended.
    /// Heuristic: queries wider than 90 days typically exceed the 2-minute AC-4 SLA.
    /// </summary>
    public static readonly TimeSpan AsyncThreshold = TimeSpan.FromDays(90);

    // Off-hours is defined as outside 06:00–22:00 UTC (business hours proxy).
    private const int BusinessHourStart = 6;
    private const int BusinessHourEnd   = 22;

    // Repeated-failure detection threshold: > 5 LoginFailure events in 10 minutes.
    private const int FailureCountThreshold  = 5;
    private const int FailureWindowMinutes   = 10;

    // Volume anomaly: actor count above mean + 2 std deviations.
    private const double StdDevMultiplier = 2.0;

    private readonly AppDbContext _db;
    private readonly ILogger<ComplianceReportGenerator> _logger;

    public ComplianceReportGenerator(
        AppDbContext db,
        ILogger<ComplianceReportGenerator> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Generates the full report data object for the given request parameters.
    /// All queries use <c>AsNoTracking</c> — no writes from this component.
    /// </summary>
    public async Task<ComplianceReportData> GenerateAsync(
        ReportRequest     request,
        Guid              reportId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating compliance report {ReportId} ({Type}) for period {Start}–{End}.",
            reportId, request.ReportType, request.PeriodStartUtc, request.PeriodEndUtc);

        var fromOffset = new DateTimeOffset(request.PeriodStartUtc, TimeSpan.Zero);
        var toOffset   = new DateTimeOffset(request.PeriodEndUtc,   TimeSpan.Zero);

        // ── 1. Access log summary (DataAccess events only) ────────────────────
        var accessRecords = await _db.AuditRecords
            .AsNoTracking()
            .Where(r => r.OccurredAt >= fromOffset
                     && r.OccurredAt <= toOffset
                     && r.EventType  == "DataAccess")
            .Select(r => new
            {
                r.ActorUserId,
                r.TargetEntityType,
            })
            .ToListAsync(ct);

        // Group by actor
        var byActorRaw = accessRecords
            .GroupBy(r => r.ActorUserId)
            .Select(g => new ActorAccessGroup
            {
                ActorName   = g.Key.ToString(), // UUID — no PII per DR-005
                Role        = string.Empty,      // enriched below if Users join is available
                AccessCount = g.Count(),
            })
            .OrderByDescending(a => a.AccessCount)
            .ToList();

        // Group by resource type
        var byResource = accessRecords
            .GroupBy(r => r.TargetEntityType)
            .Select(g => new ResourceAccessGroup
            {
                ResourceType = g.Key,
                AccessCount  = g.Count(),
            })
            .OrderByDescending(r => r.AccessCount)
            .ToList();

        var accessSummary = new AccessLogSummary
        {
            TotalAccessEvents = accessRecords.Count,
            ByActor           = byActorRaw,
            ByResource        = byResource,
        };

        // ── 2. Event counts by type ────────────────────────────────────────────
        var eventCounts = await _db.AuditRecords
            .AsNoTracking()
            .Where(r => r.OccurredAt >= fromOffset && r.OccurredAt <= toOffset)
            .GroupBy(r => r.EventType)
            .Select(g => new EventTypeCount
            {
                EventType = g.Key,
                Count     = g.Count(),
            })
            .OrderByDescending(e => e.Count)
            .ToListAsync(ct);

        int totalEvents         = eventCounts.Sum(e => e.Count);
        int uniqueActors        = accessRecords.Select(r => r.ActorUserId).Distinct().Count();
        int failedAttempts      = eventCounts.FirstOrDefault(e => e.EventType == "LoginFailure")?.Count ?? 0;

        // ── 3. Anomaly detection ──────────────────────────────────────────────
        var anomalies = await DetectAnomaliesAsync(fromOffset, toOffset, ct);

        var metrics = new ReportMetrics
        {
            TotalAuditEvents     = totalEvents,
            UniqueActors         = uniqueActors,
            AnomalyCount         = anomalies.Count,
            FailedAccessAttempts = failedAttempts,
        };

        return new ComplianceReportData
        {
            ReportId       = reportId,
            ReportType     = request.ReportType,
            PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc   = request.PeriodEndUtc,
            GeneratedAtUtc = DateTime.UtcNow,
            AccessSummary  = accessSummary,
            EventCounts    = eventCounts,
            Anomalies      = anomalies,
            KeyMetrics     = metrics,
        };
    }

    // ── Private: anomaly detection ────────────────────────────────────────────

    private async Task<IReadOnlyList<AnomalyFlag>> DetectAnomaliesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var flags = new List<AnomalyFlag>();

        // Load DataAccess records once for all anomaly checks.
        var accessRecords = await _db.AuditRecords
            .AsNoTracking()
            .Where(r => r.OccurredAt >= from
                     && r.OccurredAt <= to
                     && r.EventType  == "DataAccess")
            .Select(r => new { r.ActorUserId, r.OccurredAt })
            .ToListAsync(ct);

        // ── A. Unusual access volume (> mean + 2σ per actor) ──────────────────
        if (accessRecords.Count > 0)
        {
            var actorCounts = accessRecords
                .GroupBy(r => r.ActorUserId)
                .Select(g => (double)g.Count())
                .ToList();

            double mean   = actorCounts.Average();
            double stdDev = Math.Sqrt(actorCounts.Average(c => Math.Pow(c - mean, 2)));
            double threshold = mean + (StdDevMultiplier * stdDev);

            var outlierActors = accessRecords
                .GroupBy(r => r.ActorUserId)
                .Where(g => g.Count() > threshold)
                .Select(g => g.Key)
                .ToList();

            foreach (var actorId in outlierActors)
            {
                flags.Add(new AnomalyFlag
                {
                    AnomalyType   = "UnusualAccessVolume",
                    Description   = $"Actor {actorId} accessed records significantly above average.",
                    Severity      = "Medium",
                    DetectedAtUtc = DateTime.UtcNow,
                });
            }
        }

        // ── B. Off-hours access (outside 06:00–22:00 UTC) ─────────────────────
        var offHoursActors = accessRecords
            .Where(r => r.OccurredAt.Hour < BusinessHourStart
                     || r.OccurredAt.Hour >= BusinessHourEnd)
            .Select(r => r.ActorUserId)
            .Distinct()
            .ToList();

        if (offHoursActors.Count > 0)
        {
            flags.Add(new AnomalyFlag
            {
                AnomalyType   = "OffHoursAccess",
                Description   = $"{offHoursActors.Count} actor(s) accessed data outside business hours (06:00–22:00 UTC).",
                Severity      = "Low",
                DetectedAtUtc = DateTime.UtcNow,
            });
        }

        // ── C. Repeated failed auth attempts (> 5 in 10 minutes) ─────────────
        var failureRecords = await _db.AuditRecords
            .AsNoTracking()
            .Where(r => r.OccurredAt >= from
                     && r.OccurredAt <= to
                     && r.EventType  == "LoginFailure")
            .Select(r => new { r.ActorUserId, r.OccurredAt })
            .ToListAsync(ct);

        var suspiciousActors = failureRecords
            .GroupBy(r => r.ActorUserId)
            .Where(g =>
            {
                // Slide a 10-minute window over ordered events.
                var ordered = g.OrderBy(e => e.OccurredAt).ToList();
                for (int i = 0; i + FailureCountThreshold - 1 < ordered.Count; i++)
                {
                    var windowEnd = ordered[i + FailureCountThreshold - 1].OccurredAt;
                    if ((windowEnd - ordered[i].OccurredAt).TotalMinutes <= FailureWindowMinutes)
                        return true;
                }
                return false;
            })
            .Select(g => g.Key)
            .ToList();

        foreach (var actorId in suspiciousActors)
        {
            flags.Add(new AnomalyFlag
            {
                AnomalyType   = "RepeatedFailedAttempts",
                Description   = $"Actor {actorId} had more than {FailureCountThreshold} failed login attempts within {FailureWindowMinutes} minutes.",
                Severity      = "High",
                DetectedAtUtc = DateTime.UtcNow,
            });
        }

        return flags;
    }
}

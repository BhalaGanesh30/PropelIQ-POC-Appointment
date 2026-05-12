using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Disclosure;

/// <summary>
/// BackgroundService that compiles patient data access logs into a structured
/// disclosure report for every <see cref="DisclosureStatus.Submitted"/> request
/// (US_057, AC-2, edge case 1).
///
/// <para>
/// Poll cadence: every 30 seconds (configurable). Processes at most 10 requests per poll
/// to avoid blocking the event loop. For large time ranges (edge case 1), access log
/// rows are batched at 1 000 per DB round-trip.
/// </para>
///
/// <para>
/// State transitions owned by this worker:
/// Submitted → Compiling → PendingReview
/// </para>
/// </summary>
public sealed class DisclosureCompilationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval  = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartupDelay  = TimeSpan.FromSeconds(15);
    private const int MaxPerPoll   = 10;
    private const int AccessBatch  = 1_000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DisclosureCompilationWorker> _logger;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { WriteIndented = false };

    public DisclosureCompilationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DisclosureCompilationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DisclosureCompilationWorker started — initial delay {Delay}.", StartupDelay);
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRequestsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in DisclosureCompilationWorker poll cycle.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    // ── Core compilation logic ────────────────────────────────────────────────

    private async Task ProcessPendingRequestsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditRecordService>();

        var pending = await db.DisclosureRequests
            .Where(r => r.Status == DisclosureStatus.Submitted)
            .OrderBy(r => r.CreatedAt)
            .Take(MaxPerPoll)
            .ToListAsync(ct);

        foreach (var request in pending)
        {
            await CompileOneAsync(db, audit, request, ct);
        }
    }

    private async Task CompileOneAsync(
        AppDbContext db,
        IAuditRecordService audit,
        DisclosureRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Compiling disclosure request {RequestId} for patient {PatientId}.",
            request.Id, request.PatientId);

        request.Transition(DisclosureStatus.Compiling);
        await db.SaveChangesAsync(ct);

        try
        {
            // Edge case 1: Batch query for large date ranges to avoid memory pressure.
            var events = await FetchAccessEventsInBatchesAsync(db, request, ct);

            var report = new DisclosureReport
            {
                DisclosureRequestId = request.Id,
                AccessEventCount    = events.Count,
                ReportJson          = BuildReportJson(request, events),
            };

            db.DisclosureReports.Add(report);

            request.ReportId   = report.Id;
            request.CompiledAt = DateTimeOffset.UtcNow;
            request.Transition(DisclosureStatus.PendingReview);

            await db.SaveChangesAsync(ct);

            await audit.WriteAsync(new AuditEvent
            {
                UserId     = request.PatientId,
                EventType  = "DisclosureCompiled",
                EntityType = nameof(DisclosureRequest),
                EntityId   = request.Id,
                Details    = new Dictionary<string, object>
                {
                    ["accessEventCount"] = events.Count,
                    ["reportId"]         = report.Id.ToString(),
                },
            }, ct);

            _logger.LogInformation(
                "Disclosure request {RequestId} compiled — {Count} access events.",
                request.Id, events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to compile disclosure request {RequestId}. Reverting to Submitted for retry.",
                request.Id);

            // Revert so the next poll will retry.
            request.Transition(DisclosureStatus.Submitted);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Data fetching ─────────────────────────────────────────────────────────

    private sealed record AccessEventRow(
        Guid ActorUserId,
        string Role,
        string? FirstName,
        string? LastName,
        string TargetEntityType,
        Guid? TargetEntityId,
        DateTimeOffset OccurredAt);

    /// <summary>
    /// Streams DataAccess audit records for the patient across the requested date
    /// range in batches of <see cref="AccessBatch"/> rows (edge case 1).
    /// </summary>
    private async Task<List<AccessEventRow>> FetchAccessEventsInBatchesAsync(
        AppDbContext db,
        DisclosureRequest request,
        CancellationToken ct)
    {
        var result = new List<AccessEventRow>();
        int skip   = 0;

        while (true)
        {
            var batch = await db.AuditRecords
                .AsNoTracking()
                .Where(r =>
                    r.EventType == "DataAccess"
                    && r.PatientId == request.PatientId
                    && r.OccurredAt >= request.FromDateUtc
                    && r.OccurredAt <= request.ToDateUtc)
                .Join(db.Users,
                    r => r.ActorUserId,
                    u => u.Id,
                    (r, u) => new AccessEventRow(
                        r.ActorUserId,
                        u.Role,
                        u.FirstName,
                        u.LastName,
                        r.TargetEntityType,
                        r.TargetEntityId,
                        r.OccurredAt))
                .OrderBy(x => x.OccurredAt)
                .Skip(skip)
                .Take(AccessBatch)
                .ToListAsync(ct);

            result.AddRange(batch);
            if (batch.Count < AccessBatch) break;
            skip += AccessBatch;
        }

        return result;
    }

    // ── Report JSON builder ───────────────────────────────────────────────────

    private static string BuildReportJson(
        DisclosureRequest request,
        List<AccessEventRow> events)
    {
        var doc = new
        {
            requestId          = request.Id,
            patientId          = request.PatientId,
            generatedAtUtc     = DateTimeOffset.UtcNow.ToString("O"),
            dateRange          = new { from = request.FromDateUtc.ToString("O"), to = request.ToDateUtc.ToString("O") },
            totalAccessEvents  = events.Count,
            accessEvents       = events.Select(e => new
            {
                actorUserId      = e.ActorUserId,
                actorName        = $"{e.FirstName} {e.LastName}".Trim(),
                actorRole        = e.Role,
                resourceType     = e.TargetEntityType,
                entityId         = e.TargetEntityId?.ToString(),
                occurredAtUtc    = e.OccurredAt.ToString("O"),
            }),
        };

        return JsonSerializer.Serialize(doc, _jsonOpts);
    }
}

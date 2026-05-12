using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// Async CSV export service for audit log records (US_056, AC-3, Edge Case 2).
///
/// <para>
/// Jobs are tracked in-memory via a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Completed CSV bytes are stored in-memory with a 1-hour TTL after which the
/// download endpoint returns 404 (preventing stale data accumulation).
/// </para>
///
/// <para>
/// This is an in-process implementation suitable for the current scale.
/// At higher volumes, replace with a durable job queue (e.g., Hangfire, Azure Storage Queues).
/// </para>
/// </summary>
public sealed class AuditLogExportService
{
    private static readonly TimeSpan ExportTtl = TimeSpan.FromHours(1);

    private sealed record ExportJob(
        Guid JobId,
        DateTimeOffset CreatedAt,
        ExportJobStatus Status,
        byte[]? CsvBytes = null,
        string? ErrorMessage = null);

    private enum ExportJobStatus { Pending, Completed, Failed }

    private readonly ConcurrentDictionary<Guid, ExportJob> _jobs = new();
    private readonly AppDbContext _db;
    private readonly ILogger<AuditLogExportService> _logger;

    public AuditLogExportService(AppDbContext db, ILogger<AuditLogExportService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Starts an async CSV export job for the given query filters.
    /// Returns the job ID immediately (202 Accepted pattern).
    /// </summary>
    public Guid StartExport(AuditLogQueryRequest request)
    {
        var jobId = Guid.NewGuid();
        _jobs[jobId] = new ExportJob(jobId, DateTimeOffset.UtcNow, ExportJobStatus.Pending);

        // Fire-and-forget — the background task updates _jobs when complete.
        _ = RunExportAsync(jobId, request);

        return jobId;
    }

    /// <summary>
    /// Returns the CSV bytes if the job is complete and within TTL.
    /// Returns <c>null</c> if the job is still pending, failed, expired, or unknown.
    /// </summary>
    public (bool found, bool ready, byte[]? csvBytes) TryGetResult(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return (false, false, null);

        if (DateTimeOffset.UtcNow - job.CreatedAt > ExportTtl)
        {
            _jobs.TryRemove(jobId, out _);
            return (false, false, null);
        }

        return job.Status switch
        {
            ExportJobStatus.Completed => (true, true,  job.CsvBytes),
            ExportJobStatus.Pending   => (true, false, null),
            _                         => (true, false, null),
        };
    }

    private async Task RunExportAsync(Guid jobId, AuditLogQueryRequest request)
    {
        try
        {
            var pageSize  = Math.Clamp(request.PageSize, 1, 200);
            var fromDate  = request.From;
            var toDate    = request.To;
            var actorId   = request.ActorUserId;
            var evtType   = request.EventType;
            var entityId  = request.EntityId;

            var query =
                from audit in _db.AuditRecords
                join user in _db.Users on audit.ActorUserId equals user.Id into userGroup
                from u in userGroup.DefaultIfEmpty()
                where actorId  == null || audit.ActorUserId   == actorId.Value
                where evtType  == null || audit.EventType     == evtType
                where fromDate == null || audit.OccurredAt    >= fromDate.Value
                where toDate   == null || audit.OccurredAt    <= toDate.Value
                where entityId == null || audit.TargetEntityId == entityId.Value
                orderby audit.OccurredAt descending
                select new
                {
                    audit.Id,
                    audit.EventType,
                    audit.ActorUserId,
                    ActorFirstName   = u.FirstName,
                    ActorLastName    = u.LastName,
                    audit.TargetEntityId,
                    audit.TargetEntityType,
                    audit.OccurredAt,
                };

            var rows = await query
                .Skip(request.Page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("AuditId,EventType,ActorUserId,ActorName,EntityType,EntityId,OccurredAt");

            foreach (var row in rows)
            {
                var actorName = $"{row.ActorFirstName} {row.ActorLastName}".Trim();
                csv.AppendLine(string.Join(",",
                    CsvEscape(row.Id.ToString()),
                    CsvEscape(row.EventType),
                    CsvEscape(row.ActorUserId.ToString()),
                    CsvEscape(actorName),
                    CsvEscape(row.TargetEntityType),
                    CsvEscape(row.TargetEntityId?.ToString() ?? string.Empty),
                    CsvEscape(row.OccurredAt.ToString("O", CultureInfo.InvariantCulture))));
            }

            var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
            _jobs[jobId] = _jobs[jobId] with { Status = ExportJobStatus.Completed, CsvBytes = csvBytes };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log export job {JobId} failed.", jobId);
            _jobs[jobId] = _jobs[jobId] with
            {
                Status       = ExportJobStatus.Failed,
                ErrorMessage = ex.Message,
            };
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

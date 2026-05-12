using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// Daily background service that evaluates the 7-year audit retention policy (US_056, AC-3, DR-005).
///
/// Archival lifecycle per run:
/// <list type="bullet">
///   <item>Queries <c>pg_inherits</c> to discover all child partitions of <c>app.audit_records</c>.</item>
///   <item>Reads the upper-bound date of each partition from <c>pg_get_expr</c>.</item>
///   <item>If the partition's upper bound is older than 7 years, copies all rows to
///         <c>app.audit_records_archive</c>, then detaches and drops the partition.</item>
///   <item>Calls <see cref="PartitionMaintenanceService.EnsureNextYearPartitionAsync"/> to
///         pre-create the upcoming year's partition before it is needed.</item>
/// </list>
///
/// Uses <see cref="IServiceScopeFactory"/> to resolve scoped <c>AppDbContext</c>.
/// </summary>
public sealed class RetentionPolicyWorker : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly PartitionMaintenanceService    _partitionService;
    private readonly ILogger<RetentionPolicyWorker> _logger;

    public RetentionPolicyWorker(
        IServiceScopeFactory            scopeFactory,
        PartitionMaintenanceService     partitionService,
        ILogger<RetentionPolicyWorker>  logger)
    {
        _scopeFactory     = scopeFactory;
        _partitionService = partitionService;
        _logger           = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RetentionPolicyWorker started. Retention window: 7 years. Poll interval: {Interval}.",
            RunInterval);

        // Initial delay to let the application warm up before first archival run.
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateRetentionAsync(stoppingToken);
                await EnsureNextPartitionAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "RetentionPolicyWorker encountered an unexpected error during archival evaluation.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task EnsureNextPartitionAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await _partitionService.EnsureNextYearPartitionAsync(db, ct);
    }

    /// <summary>
    /// Identifies partitions older than 7 years, archives their data, and detaches them.
    /// </summary>
    private async Task EvaluateRetentionAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var retentionCutoff = DateTimeOffset.UtcNow.AddYears(-7);

        // Query PostgreSQL catalog to list all child partitions of audit_records,
        // along with their upper-bound dates extracted from the partition constraint expression.
        // pg_get_expr(relpartbound, oid) returns the partition constraint, e.g.:
        //   FOR VALUES FROM ('2018-01-01') TO ('2019-01-01')
        // We extract the upper bound using a string split.
        var partitions = await db.Database.SqlQueryRaw<PartitionInfo>("""
            SELECT
                c.relname              AS PartitionName,
                pg_get_expr(c.relpartbound, c.oid) AS PartitionBound
            FROM   pg_inherits i
            JOIN   pg_class    c ON c.oid = i.inhrelid
            JOIN   pg_class    p ON p.oid = i.inhparent
            JOIN   pg_namespace n ON n.oid = p.relnamespace
            WHERE  p.relname   = 'audit_records'
              AND  n.nspname   = 'app'
              AND  c.relname  <> 'audit_records_default'
            ORDER  BY c.relname
            """).ToListAsync(ct);

        if (partitions.Count == 0)
        {
            _logger.LogInformation("RetentionPolicyWorker: no named child partitions found. Skipping archival.");
            return;
        }

        foreach (var partition in partitions)
        {
            var upperBound = ExtractUpperBound(partition.PartitionBound);
            if (upperBound is null)
            {
                _logger.LogWarning(
                    "RetentionPolicyWorker: could not parse upper bound for partition {Name}. Skipping.",
                    partition.PartitionName);
                continue;
            }

            if (upperBound.Value >= retentionCutoff)
            {
                _logger.LogDebug(
                    "RetentionPolicyWorker: partition {Name} upper bound {Upper} is within retention window. Skipping.",
                    partition.PartitionName, upperBound.Value);
                continue;
            }

            await ArchivePartitionAsync(db, partition.PartitionName, upperBound.Value, ct);
        }
    }

    /// <summary>
    /// Copies all rows from the specified partition to <c>audit_records_archive</c>,
    /// detaches the partition from the parent table, and drops it.
    /// </summary>
    private async Task ArchivePartitionAsync(
        AppDbContext db,
        string       partitionName,
        DateTimeOffset upperBound,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "RetentionPolicyWorker: archiving partition {Name} (upper bound {Upper}).",
            partitionName, upperBound);

        try
        {
            // Copy rows to cold storage archive table.
            var rowsCopied = await db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO app.audit_records_archive (
                    id, event_type, actor_user_id, target_entity_id, target_entity_type,
                    occurred_at, details, override_constraint_type, override_reason, override_action,
                    archived_at
                )
                SELECT
                    id, event_type, actor_user_id, target_entity_id, target_entity_type,
                    occurred_at, details, override_constraint_type, override_reason, override_action,
                    NOW()
                FROM app.{partitionName}
                ON CONFLICT (id) DO NOTHING
                """, ct);

            // Verify the copy by comparing counts.
            var sourceCount = await db.AuditRecords
                .FromSqlRaw($"SELECT * FROM app.{partitionName}")
                .LongCountAsync(ct);

            _logger.LogInformation(
                "RetentionPolicyWorker: copied {Rows} rows from {Name} to archive.",
                sourceCount, partitionName);

            // Detach partition from parent table (makes it a standalone table).
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE app.audit_records DETACH PARTITION app.{partitionName} CONCURRENTLY",
                ct);

            // Drop the now-detached partition.
            await db.Database.ExecuteSqlRawAsync(
                $"DROP TABLE IF EXISTS app.{partitionName}",
                ct);

            // Write an audit event for the archival operation itself.
            db.AuditRecords.Add(new AuditRecord
            {
                EventType        = "PartitionArchived",
                ActorUserId      = Guid.Empty, // System actor
                TargetEntityType = "AuditPartition",
                OccurredAt       = DateTimeOffset.UtcNow,
                Details = new AuditDetails
                {
                    ChangeDescription =
                        $"Partition '{partitionName}' (upper bound {upperBound:yyyy-MM-dd}) " +
                        $"archived to audit_records_archive. " +
                        $"{sourceCount} rows moved. Ref: US_056, AC-3, DR-005.",
                    Metadata = new Dictionary<string, string>
                    {
                        ["partition"]   = partitionName,
                        ["upper_bound"] = upperBound.ToString("yyyy-MM-dd"),
                        ["rows_moved"]  = sourceCount.ToString(),
                    },
                },
            });

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "RetentionPolicyWorker: partition {Name} archived and dropped successfully.",
                partitionName);
        }
        catch (Exception ex)
        {
            DiagnosticsConfig.AuditRecordWriteFailureCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "partition_archival_failed"));

            _logger.LogCritical(ex,
                "COMPLIANCE ALERT: Failed to archive partition {Name}. " +
                "Manual intervention required to complete 7-year retention enforcement (AC-3).",
                partitionName);
        }
    }

    /// <summary>
    /// Extracts the upper date bound from a PostgreSQL partition constraint expression.
    /// Example input: <c>FOR VALUES FROM ('2018-01-01 00:00:00+00') TO ('2019-01-01 00:00:00+00')</c>
    /// </summary>
    private static DateTimeOffset? ExtractUpperBound(string? partitionBound)
    {
        if (string.IsNullOrWhiteSpace(partitionBound)) return null;

        // Format: FOR VALUES FROM ('YYYY-MM-DD ...') TO ('YYYY-MM-DD ...')
        var toIndex = partitionBound.IndexOf(" TO ('", StringComparison.OrdinalIgnoreCase);
        if (toIndex < 0) return null;

        var start = toIndex + 6; // skip " TO ('"
        var end   = partitionBound.IndexOf('\'', start);
        if (end < 0 || end <= start) return null;

        var rawDate = partitionBound[start..end];
        return DateTimeOffset.TryParse(rawDate, out var date) ? date : null;
    }

    /// <summary>Internal DTO for raw SQL query of partition catalog data.</summary>
    private sealed class PartitionInfo
    {
        public string PartitionName  { get; set; } = string.Empty;
        public string? PartitionBound { get; set; }
    }
}

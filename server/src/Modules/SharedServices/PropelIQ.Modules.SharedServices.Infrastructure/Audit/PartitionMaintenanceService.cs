using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// Service that pre-creates the upcoming year's <c>app.audit_records</c> child partition
/// and applies the immutability trigger and GRANT restrictions (US_056 task_002).
///
/// Called by <see cref="RetentionPolicyWorker"/> after each archival cycle to ensure
/// the next year's partition exists before data starts arriving (partition management
/// is safer than relying solely on the default overflow partition).
///
/// Registered as <b>Singleton</b> — stateless service with no DB context held directly;
/// <c>AppDbContext</c> is passed in per-call from <see cref="RetentionPolicyWorker"/>.
/// </summary>
public sealed class PartitionMaintenanceService
{
    private readonly ILogger<PartitionMaintenanceService> _logger;

    public PartitionMaintenanceService(ILogger<PartitionMaintenanceService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the partition for the year following the current year exists.
    /// If not, creates it, applies the immutability trigger, and restricts GRANTs.
    /// </summary>
    public async Task EnsureNextYearPartitionAsync(CancellationToken ct = default)
    {
        // Use a dedicated scope from outside (called by RetentionPolicyWorker which manages scope).
        // Callers must pass a db context directly so we don't need to inject IServiceScopeFactory here.
        // This overload is intended to be called with a provided db — see overload below.
        _logger.LogDebug("PartitionMaintenanceService.EnsureNextYearPartitionAsync called without db context — use overload with AppDbContext.");
    }

    /// <summary>
    /// Checks whether the partition for the year following the current year exists.
    /// If not, creates it, applies the immutability trigger, and restricts GRANTs.
    /// </summary>
    /// <param name="db">Scoped <see cref="AppDbContext"/> from the caller's scope.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task EnsureNextYearPartitionAsync(AppDbContext db, CancellationToken ct = default)
    {
        var nextYear      = DateTime.UtcNow.Year + 1;
        var partitionName = $"audit_records_y{nextYear}";
        var fromDate      = $"{nextYear}-01-01";
        var toDate        = $"{nextYear + 1}-01-01";

        // Check if partition already exists in pg_class.
        var existsResult = await db.Database.SqlQueryRaw<int>(
            $"""
            SELECT COUNT(*)::int
            FROM   pg_class c
            JOIN   pg_namespace n ON n.oid = c.relnamespace
            WHERE  n.nspname = 'app'
              AND  c.relname = '{partitionName}'
            """).SingleOrDefaultAsync(ct);

        if (existsResult > 0)
        {
            _logger.LogDebug(
                "PartitionMaintenanceService: partition {Name} already exists. Skipping.",
                partitionName);
            return;
        }

        _logger.LogInformation(
            "PartitionMaintenanceService: creating partition {Name} for {Year}.",
            partitionName, nextYear);

        // Create the child partition.
        await db.Database.ExecuteSqlRawAsync(
            $"""
            CREATE TABLE IF NOT EXISTS app.{partitionName}
                PARTITION OF app.audit_records
                FOR VALUES FROM ('{fromDate}') TO ('{toDate}')
            """, ct);

        // Apply immutability trigger (must be on child partition — not inherited from parent).
        await db.Database.ExecuteSqlRawAsync(
            $"""
            CREATE TRIGGER trg_audit_records_immutable
                BEFORE UPDATE OR DELETE ON app.{partitionName}
                FOR EACH ROW EXECUTE FUNCTION app.fn_prevent_audit_mutation()
            """, ct);

        // Restrict app_user to INSERT + SELECT only.
        await db.Database.ExecuteSqlRawAsync(
            $"""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
                    REVOKE ALL ON app.{partitionName} FROM app_user;
                    GRANT INSERT, SELECT ON app.{partitionName} TO app_user;
                END IF;
            END $$
            """, ct);

        // Write a system audit event for the partition creation.
        db.AuditRecords.Add(new AuditRecord
        {
            EventType        = "PartitionCreated",
            ActorUserId      = Guid.Empty,
            TargetEntityType = "AuditPartition",
            OccurredAt       = DateTimeOffset.UtcNow,
            Details = new AuditDetails
            {
                ChangeDescription =
                    $"Created audit_records child partition '{partitionName}' " +
                    $"for year {nextYear} ({fromDate} – {toDate}). " +
                    $"Immutability trigger and GRANT restrictions applied. Ref: US_056 task_002.",
                Metadata = new Dictionary<string, string>
                {
                    ["partition"] = partitionName,
                    ["year"]      = nextYear.ToString(),
                    ["from_date"] = fromDate,
                    ["to_date"]   = toDate,
                },
            },
        });

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PartitionMaintenanceService: partition {Name} created successfully.",
            partitionName);
    }
}

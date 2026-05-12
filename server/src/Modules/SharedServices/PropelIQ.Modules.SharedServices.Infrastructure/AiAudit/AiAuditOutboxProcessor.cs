using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.AiAudit;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.SharedServices.Infrastructure.AiAudit;

/// <summary>
/// Background service that retries failed AI audit log writes from the <c>ai_audit_outbox</c>
/// table every 60 seconds (US_055, Edge Case 1).
///
/// Retry strategy:
/// <list type="bullet">
///   <item>Fetches up to 20 outbox records where <c>retry_count &lt; 3</c>.</item>
///   <item>For each record, deserializes the payload and re-attempts the primary INSERT.</item>
///   <item>On success: deletes the outbox record.</item>
///   <item>On failure: increments <c>retry_count</c> and sets <c>last_attempt_at = now()</c>.</item>
///   <item>After 3 retries: the record is left in place and the <c>compliance.audit_write_failure</c>
///         counter is incremented for operations alerting. No further automatic retries.</item>
/// </list>
///
/// Uses <see cref="IServiceScopeFactory"/> to resolve scoped <c>AppDbContext</c> from
/// the long-lived hosted service scope.
/// </summary>
internal sealed class AiAuditOutboxProcessor : BackgroundService
{
    private static readonly TimeSpan Interval  = TimeSpan.FromSeconds(60);
    private const           int      BatchSize = 20;
    private const           int      MaxRetries = 3;

    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<AiAuditOutboxProcessor> _logger;

    public AiAuditOutboxProcessor(
        IServiceScopeFactory           scopeFactory,
        ILogger<AiAuditOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AiAuditOutboxProcessor batch failed unexpectedly.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.AiAuditOutbox
            .Where(o => o.RetryCount < MaxRetries)
            .OrderBy(o => o.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        _logger.LogInformation(
            "AiAuditOutboxProcessor processing {Count} pending outbox records.",
            pending.Count);

        foreach (var outboxEntry in pending)
        {
            AiAuditEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<AiAuditEntry>(outboxEntry.Payload);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize outbox payload for OutboxId {OutboxId}. " +
                    "Skipping (manual cleanup required).",
                    outboxEntry.OutboxId);
                continue;
            }

            if (entry is null)
            {
                _logger.LogWarning(
                    "Null payload deserialized for OutboxId {OutboxId}. Skipping.",
                    outboxEntry.OutboxId);
                continue;
            }

            try
            {
                await RetryPrimaryWriteAsync(db, entry, ct);

                db.AiAuditOutbox.Remove(outboxEntry);
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Outbox retry succeeded for AiRequestId {AiRequestId}. Outbox record removed.",
                    entry.AiRequestId);
            }
            catch (Exception ex)
            {
                outboxEntry.RetryCount++;
                outboxEntry.LastAttemptAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                _logger.LogWarning(ex,
                    "Outbox retry {RetryCount}/{MaxRetries} failed for AiRequestId {AiRequestId}.",
                    outboxEntry.RetryCount, MaxRetries, entry.AiRequestId);

                if (outboxEntry.RetryCount >= MaxRetries)
                {
                    _logger.LogCritical(
                        "AiRequestId {AiRequestId} exhausted all {MaxRetries} retries. " +
                        "Audit record LOST. Manual investigation required (Edge Case 1, US_055).",
                        entry.AiRequestId, MaxRetries);

                    DiagnosticsConfig.AuditWriteFailureCounter.Add(1,
                        new KeyValuePair<string, object?>("reason", "max_retries_exhausted"));
                }
            }
        }
    }

    private static async Task RetryPrimaryWriteAsync(
        AppDbContext    db,
        AiAuditEntry    entry,
        CancellationToken ct)
    {
        var entity = new Domain.Entities.AiAuditLogEntity
        {
            AiRequestId      = entry.AiRequestId,
            RequestTimestamp = entry.RequestTimestamp,
            ClinicianId      = entry.ClinicianId,
            PromptHash       = entry.PromptHash,
            ContextRefs      = entry.ContextRefs,
            ModelName        = entry.ModelName,
            ResponsePayload  = entry.ResponsePayload,
            ConfidenceScores = entry.ConfidenceScores,
            LatencyMs        = entry.LatencyMs,
            FallbackReason   = entry.FallbackReason,
            CreatedAt        = DateTimeOffset.UtcNow,
        };

        db.AiAuditLogs.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Resilience;
using Polly;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// Background service that retries entries in <c>app.audit_dead_letters</c> every
/// 5 minutes (US_056, AC-2, Edge Case 1).
///
/// Retry lifecycle:
/// <list type="bullet">
///   <item>Query: <c>WHERE resolved_at IS NULL AND retry_count &lt; 5</c> ordered by <c>created_at</c>.</item>
///   <item>Per entry: deserialize payload → map to <see cref="AuditRecord"/> → INSERT via EF Core.</item>
///   <item>On success: set <c>resolved_at = UtcNow</c>.</item>
///   <item>On failure: increment <c>retry_count</c>, set <c>last_retry_at = UtcNow</c>.</item>
///   <item>After <c>retry_count = 5</c>: <c>LogCritical</c> compliance alert; no further retries.</item>
/// </list>
///
/// Exponential backoff is applied per-entry via Polly so transient DB failures
/// don't immediately burn through retry budget.
/// </summary>
internal sealed class DeadLetterRetryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private const           int      MaxRetries   = 5;
    private const           int      BatchSize    = 50;

    private static readonly ResiliencePipeline _retryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(2),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
            })
            .Build();

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { WriteIndented = false };

    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<DeadLetterRetryWorker> _logger;

    public DeadLetterRetryWorker(
        IServiceScopeFactory            scopeFactory,
        ILogger<DeadLetterRetryWorker>  logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadLetterRetryWorker started with {Interval} poll interval.", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "DeadLetterRetryWorker batch failed unexpectedly.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.AuditDeadLetters
            .Where(d => d.ResolvedAt == null && d.RetryCount < MaxRetries)
            .OrderBy(d => d.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        _logger.LogInformation(
            "DeadLetterRetryWorker processing {Count} dead-letter entries.",
            pending.Count);

        foreach (var entry in pending)
        {
            await RetryEntryAsync(db, entry, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RetryEntryAsync(AppDbContext db, AuditDeadLetter entry, CancellationToken ct)
    {
        try
        {
            AuditEvent? auditEvent;
            try
            {
                auditEvent = JsonSerializer.Deserialize<AuditEvent>(entry.Payload, _jsonOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx,
                    "Dead-letter entry {Id} has invalid JSON payload. Skipping.", entry.Id);
                return;
            }

            if (auditEvent is null)
            {
                _logger.LogWarning(
                    "Dead-letter entry {Id} deserialized to null. Skipping.", entry.Id);
                return;
            }

            await _retryPipeline.ExecuteAsync(async resolverCt =>
            {
                var record = new AuditRecord
                {
                    EventType        = auditEvent.EventType,
                    ActorUserId      = auditEvent.UserId,
                    TargetEntityId   = auditEvent.EntityId,
                    TargetEntityType = auditEvent.EntityType,
                    OccurredAt       = auditEvent.OccurredAt,
                    Details = new AuditDetails
                    {
                        ChangeDescription =
                            $"[Replayed from dead-letter {entry.Id}] Event '{auditEvent.EventType}' " +
                            $"by {auditEvent.UserId} on {auditEvent.EntityType} at {auditEvent.OccurredAt:O}.",
                        Metadata = auditEvent.Details.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value?.ToString() ?? string.Empty),
                    },
                };

                db.AuditRecords.Add(record);
                await db.SaveChangesAsync(resolverCt);
            }, ct);

            entry.ResolvedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Dead-letter entry {Id} resolved successfully on retry {Count}.",
                entry.Id, entry.RetryCount + 1);
        }
        catch (Exception ex)
        {
            entry.RetryCount++;
            entry.LastRetryAt = DateTimeOffset.UtcNow;

            if (entry.RetryCount >= MaxRetries)
            {
                DiagnosticsConfig.AuditRecordWriteFailureCounter.Add(1,
                    new KeyValuePair<string, object?>("reason", "dead_letter_max_retries_exhausted"));

                _logger.LogCritical(
                    "COMPLIANCE ALERT: Dead-letter entry {Id} has exhausted all {MaxRetries} retries. " +
                    "Audit event for {EventType} by actor cannot be recovered automatically. " +
                    "Manual investigation required (US_056 Edge Case 1).",
                    entry.Id, MaxRetries, entry.Payload[..Math.Min(100, entry.Payload.Length)]);
            }
            else
            {
                _logger.LogWarning(ex,
                    "Dead-letter entry {Id} retry {RetryCount}/{MaxRetries} failed.",
                    entry.Id, entry.RetryCount, MaxRetries);
            }
        }
    }
}

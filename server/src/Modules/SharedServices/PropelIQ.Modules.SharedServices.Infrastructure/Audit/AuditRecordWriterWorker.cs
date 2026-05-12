using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// BackgroundService that drains the <see cref="AuditRecordChannel"/> and persists
/// each <see cref="AuditEvent"/> to <c>app.audit_records</c> (US_056, AC-1, AC-2).
///
/// <para>
/// A scoped <see cref="AppDbContext"/> is created per write via <see cref="IServiceScopeFactory"/>
/// (TR-005 — BackgroundServices must not hold long-lived scoped services).
/// </para>
///
/// <para>
/// On <see cref="DbUpdateException"/>, the event is written to <c>app.audit_dead_letters</c>
/// instead and a warning is logged (AC-2). The caller's write to the channel is unaffected.
/// </para>
/// </summary>
public sealed class AuditRecordWriterWorker : BackgroundService
{
    // ── Metrics ──────────────────────────────────────────────────────────────
    private static readonly Counter<long> _writtenCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "audit.records.written",
            unit: "{records}",
            description: "Total audit records successfully persisted.");

    private static readonly Counter<long> _deadLetterCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "audit.records.dead_lettered",
            unit: "{records}",
            description: "Total audit records moved to dead-letter after DB failure.");
    // ─────────────────────────────────────────────────────────────────────────

    private readonly AuditRecordChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRecordWriterWorker> _logger;

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { WriteIndented = false };

    public AuditRecordWriterWorker(
        AuditRecordChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditRecordWriterWorker> logger)
    {
        _channel     = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditRecordWriterWorker started.");

        await foreach (var auditEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await PersistEventAsync(auditEvent, stoppingToken);
        }

        _logger.LogInformation("AuditRecordWriterWorker stopped.");
    }

    private async Task PersistEventAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var record = MapToRecord(auditEvent);
            db.AuditRecords.Add(record);
            await db.SaveChangesAsync(ct);

            _writtenCounter.Add(1,
                new KeyValuePair<string, object?>("event_type", auditEvent.EventType));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Audit record write failed for {EventType} by actor {ActorUserId}. " +
                "Moving to dead-letter store (US_056 AC-2).",
                auditEvent.EventType,
                auditEvent.UserId);

            await WriteToDeadLetterAsync(db, auditEvent, ex.Message, ct);

            _deadLetterCounter.Add(1,
                new KeyValuePair<string, object?>("event_type", auditEvent.EventType));
        }
    }

    private static AuditRecord MapToRecord(AuditEvent evt) =>
        new()
        {
            EventType        = evt.EventType,
            ActorUserId      = evt.UserId,
            TargetEntityId   = evt.EntityId,
            TargetEntityType = evt.EntityType,
            OccurredAt       = evt.OccurredAt,
            PatientId        = ExtractPatientId(evt.Details),
            Details = new AuditDetails
            {
                ChangeDescription = $"Event '{evt.EventType}' by {evt.UserId} on {evt.EntityType} at {evt.OccurredAt:O}.",
                Metadata          = evt.Details.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? string.Empty),
            },
        };

    /// <summary>
    /// Extracts the <c>patientId</c> key from the event details dictionary when present.
    /// Supports both <see cref="Guid"/> values (typed write) and string representations
    /// (emitted by <c>PatientDataAccessFilter</c>). Returns null for non-patient events.
    /// </summary>
    private static Guid? ExtractPatientId(Dictionary<string, object> details)
    {
        if (!details.TryGetValue("patientId", out var raw)) return null;
        return raw switch
        {
            Guid g       => g,
            string s     => Guid.TryParse(s, out var id) ? id : null,
            _            => null,
        };
    }

    private async Task WriteToDeadLetterAsync(
        AppDbContext db,
        AuditEvent evt,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            var deadLetter = new AuditDeadLetter
            {
                Payload      = JsonSerializer.Serialize(evt, _jsonOptions),
                ErrorMessage = errorMessage.Length > 2000
                    ? errorMessage[..2000]
                    : errorMessage,
            };

            db.AuditDeadLetters.Add(deadLetter);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception innerEx)
        {
            // If even the dead-letter write fails, log Critical and abandon.
            // This is the last line of defense — the audit event is irrecoverably lost.
            // Raising an exception here would terminate the BackgroundService loop.
            DiagnosticsConfig.AuditRecordWriteFailureCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "dead_letter_write_failed"));

            _logger.LogCritical(innerEx,
                "CRITICAL: Dead-letter write also failed for AuditEvent {EventType} " +
                "actor {ActorUserId}. Audit event LOST. Manual investigation required (US_056 Edge Case 1).",
                evt.EventType, evt.UserId);
        }
    }
}

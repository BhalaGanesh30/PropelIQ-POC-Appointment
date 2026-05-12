using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.AiAudit;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.SharedServices.Infrastructure.AiAudit;

/// <summary>
/// Append-only AI audit service (US_055, AIR-011).
///
/// All writes are INSERT-only. No UPDATE or DELETE is ever issued to <c>ai_audit_logs</c>
/// or <c>ai_audit_log_outcomes</c> (AC-3, DR-005, NFR-010).
///
/// On primary write failure, the entry is written to <c>ai_audit_outbox</c>
/// for retry by <see cref="AiAuditOutboxProcessor"/> (Edge Case 1).
/// </summary>
internal sealed class AiAuditService : IAiAuditService
{
    private readonly AppDbContext           _db;
    private readonly ILogger<AiAuditService> _logger;

    public AiAuditService(AppDbContext db, ILogger<AiAuditService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAiRequestAsync(AiAuditEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entity = new AiAuditLogEntity
        {
            AiRequestId       = entry.AiRequestId,
            RequestTimestamp  = entry.RequestTimestamp,
            ClinicianId       = entry.ClinicianId,
            PromptHash        = entry.PromptHash,
            ContextRefs       = entry.ContextRefs,
            ModelName         = entry.ModelName,
            ResponsePayload   = entry.ResponsePayload,
            ConfidenceScores  = entry.ConfidenceScores,
            LatencyMs         = entry.LatencyMs,
            FallbackReason    = entry.FallbackReason,
            CreatedAt         = DateTimeOffset.UtcNow,
        };

        try
        {
            _db.AiAuditLogs.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Primary AI audit write failed for AiRequestId {AiRequestId}. " +
                "Falling back to outbox (Edge Case 1, US_055).",
                entry.AiRequestId);

            DiagnosticsConfig.AuditWriteFailureCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "primary_write_failed"));

            await WriteOutboxAsync(entry, ct);
        }
    }

    /// <inheritdoc />
    public async Task AppendReviewerOutcomeAsync(
        Guid             aiRequestId,
        string           reviewerAction,
        string?          reviewerNote,
        CancellationToken ct = default)
    {
        var outcome = new AiAuditLogOutcomeEntity
        {
            OutcomeId      = Guid.NewGuid(),
            AiRequestId    = aiRequestId,
            ReviewerAction = reviewerAction,
            ReviewerNote   = reviewerNote,
            DecidedAt      = DateTimeOffset.UtcNow,
        };

        _db.AiAuditLogOutcomes.Add(outcome);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiAuditLogDto>> QueryAsync(
        Guid?            clinicianId,
        DateTimeOffset?  from,
        DateTimeOffset?  to,
        int              pageSize,
        int              page,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AiAuditLogs.AsNoTracking();

        if (clinicianId.HasValue)
            query = query.Where(a => a.ClinicianId == clinicianId.Value);

        if (from.HasValue)
            query = query.Where(a => a.RequestTimestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.RequestTimestamp <= to.Value);

        var results = await query
            .OrderByDescending(a => a.RequestTimestamp)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(a => new AiAuditLogDto(
                a.AiRequestId,
                a.RequestTimestamp,
                a.ClinicianId,
                a.PromptHash,
                a.ModelName,
                a.LatencyMs,
                a.FallbackReason,
                a.CreatedAt))
            .ToListAsync(ct);

        return results;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task WriteOutboxAsync(AiAuditEntry entry, CancellationToken ct)
    {
        // Use a fresh SaveChanges scope so a prior transaction rollback does not block us.
        try
        {
            var payload = JsonSerializer.Serialize(entry);
            var outbox  = new AiAuditOutboxEntity
            {
                OutboxId    = Guid.NewGuid(),
                AiRequestId = entry.AiRequestId,
                Payload     = payload,
                RetryCount  = 0,
                CreatedAt   = DateTimeOffset.UtcNow,
            };

            // Detach the failed entity so EF does not re-try it on this SaveChanges.
            _db.ChangeTracker.Clear();

            _db.AiAuditOutbox.Add(outbox);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception outboxEx)
        {
            // Both primary and outbox writes failed — emit alert metric (Edge Case 1).
            _logger.LogCritical(outboxEx,
                "Outbox write also failed for AiRequestId {AiRequestId}. " +
                "Audit record LOST. Manual investigation required (Edge Case 1, US_055).",
                entry.AiRequestId);

            DiagnosticsConfig.AuditWriteFailureCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "outbox_write_failed"));
        }
    }

    /// <summary>
    /// Computes SHA-256 hex of a string.  Used by callers to hash the redacted prompt
    /// before passing to <see cref="LogAiRequestAsync"/> (no raw PII ever stored).
    /// </summary>
    internal static string ComputePromptHash(string redactedPrompt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(redactedPrompt));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

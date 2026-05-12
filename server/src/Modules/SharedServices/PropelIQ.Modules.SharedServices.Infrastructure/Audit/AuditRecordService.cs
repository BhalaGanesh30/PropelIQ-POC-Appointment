using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// Channel-based implementation of <see cref="IAuditRecordService"/> (US_056, AC-1).
///
/// Writes to the <see cref="AuditRecordChannel"/> singleton without touching the database.
/// The <c>AuditRecordWriterWorker</c> BackgroundService drains the channel and persists
/// each event to <c>app.audit_records</c>.
///
/// Registered as <b>Singleton</b> because <see cref="AuditRecordChannel"/> is singleton
/// and no scoped/transient services are consumed here.
/// </summary>
public sealed class AuditRecordService : IAuditRecordService
{
    private readonly AuditRecordChannel _channel;

    public AuditRecordService(AuditRecordChannel channel)
    {
        _channel = channel;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return _channel.Writer.WriteAsync(auditEvent, ct);
    }
}

namespace PropelIQ.Modules.SharedServices.Application.Audit;

/// <summary>
/// Channel-based write interface for fire-and-forget audit event persistence (US_056, AC-1).
///
/// <para>
/// Callers post an <see cref="AuditEvent"/> to an in-process bounded channel
/// (capacity 10,000, <c>BoundedChannelFullMode.Wait</c>) and return immediately.
/// The <c>AuditRecordWriterWorker</c> BackgroundService drains the channel and
/// persists each event to <c>app.audit_records</c>.
/// </para>
///
/// <para>
/// This interface is deliberately separate from <see cref="IAuditService"/> which
/// handles specialised transactional audit events (Override, StaffBooking).
/// <c>IAuditRecordService</c> is for high-frequency, fire-and-forget events across
/// all modules.
/// </para>
/// </summary>
public interface IAuditRecordService
{
    /// <summary>
    /// Enqueues an audit event onto the in-process channel.
    ///
    /// Awaiting this method provides back-pressure when the channel is full but
    /// never calls into the database — write latency is bounded by channel throughput.
    ///
    /// Implementations must be thread-safe (channel writes are inherently thread-safe).
    /// </summary>
    /// <param name="auditEvent">The event to persist. Must not be <c>null</c>.</param>
    /// <param name="ct">Propagated cancellation token. A cancelled token aborts the enqueue.</param>
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken ct = default);
}

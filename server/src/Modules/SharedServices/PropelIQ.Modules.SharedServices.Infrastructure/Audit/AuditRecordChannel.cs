using System.Threading.Channels;
using PropelIQ.Modules.SharedServices.Application.Audit;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Audit;

/// <summary>
/// Singleton bounded channel that decouples audit event producers from DB persistence.
///
/// Capacity of 10,000 with <see cref="BoundedChannelFullMode.Wait"/> applies back-pressure
/// to callers under heavy load rather than dropping events silently (AC-1).
/// <c>SingleReader = true</c> enables lock-free fast path inside <see cref="System.Threading.Channels.Channel{T}"/>
/// since only <c>AuditRecordWriterWorker</c> reads from this channel.
/// </summary>
public sealed class AuditRecordChannel
{
    private readonly Channel<AuditEvent> _channel;

    public AuditRecordChannel()
    {
        _channel = Channel.CreateBounded<AuditEvent>(new BoundedChannelOptions(10_000)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Write end used by <see cref="AuditRecordService"/> to enqueue events.</summary>
    public ChannelWriter<AuditEvent> Writer => _channel.Writer;

    /// <summary>Read end consumed by <c>AuditRecordWriterWorker</c>.</summary>
    public ChannelReader<AuditEvent> Reader => _channel.Reader;
}

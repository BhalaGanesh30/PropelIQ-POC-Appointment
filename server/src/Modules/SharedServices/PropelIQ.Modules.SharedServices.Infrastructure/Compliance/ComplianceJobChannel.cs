using System.Threading.Channels;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Compliance;

/// <summary>
/// Singleton bounded channel that decouples the API controller from the async
/// compliance report job processor (US_058, edge case 1).
///
/// Capacity of 50 with <see cref="BoundedChannelFullMode.Wait"/> applies back-pressure.
/// <c>SingleReader = true</c> enables lock-free fast path because only
/// <see cref="ComplianceReportJobWorker"/> reads from this channel.
/// </summary>
public sealed class ComplianceJobChannel
{
    private readonly Channel<Guid> _channel;

    public ComplianceJobChannel()
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(50)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Write end used by <see cref="ComplianceReportService"/> to enqueue job IDs.</summary>
    public ChannelWriter<Guid> Writer => _channel.Writer;

    /// <summary>Read end consumed by <see cref="ComplianceReportJobWorker"/>.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;
}

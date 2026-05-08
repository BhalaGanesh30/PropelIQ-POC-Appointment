using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;

/// <summary>
/// Singleton in-process channel that decouples the document upload flow from OCR execution.
///
/// Bounded capacity is set to <c>ConcurrencyLimit × 10</c>.  When the queue is full,
/// <see cref="BoundedChannelFullMode.Wait"/> applies back-pressure to writers, preventing
/// unbounded memory growth under high upload load (Edge Case 2).
///
/// <c>SingleReader = false</c> allows <see cref="OcrWorkerService"/> to spawn
/// multiple concurrent consumer tasks up to <c>ConcurrencyLimit</c>.
/// </summary>
public sealed class OcrJobChannel
{
    private readonly Channel<OcrJob> _channel;

    public OcrJobChannel(IOptions<OcrConfiguration> options)
    {
        var cfg = options.Value;
        var capacity = cfg.ConcurrencyLimit * 10;

        _channel = Channel.CreateBounded<OcrJob>(new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    /// <summary>Write end used by the upload service to enqueue new OCR jobs.</summary>
    public ChannelWriter<OcrJob> Writer => _channel.Writer;

    /// <summary>Read end consumed by <see cref="OcrWorkerService"/> worker tasks.</summary>
    public ChannelReader<OcrJob> Reader => _channel.Reader;
}

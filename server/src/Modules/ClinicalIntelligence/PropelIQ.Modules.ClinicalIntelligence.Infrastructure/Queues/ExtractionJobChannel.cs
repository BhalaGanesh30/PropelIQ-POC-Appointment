using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;

/// <summary>
/// Singleton in-process channel that decouples OCR completion from extraction pipeline execution.
/// Bounded capacity is set to <c>ConcurrencyLimit × 10</c>.
/// </summary>
public sealed class ExtractionJobChannel
{
    private readonly Channel<ExtractionJob> _channel;

    public ExtractionJobChannel(IOptions<ExtractionConfiguration> options)
    {
        var cfg = options.Value;
        var capacity = cfg.ConcurrencyLimit * 10;

        _channel = Channel.CreateBounded<ExtractionJob>(new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    /// <summary>Write end used to enqueue new extraction jobs.</summary>
    public ChannelWriter<ExtractionJob> Writer => _channel.Writer;

    /// <summary>Read end consumed by ExtractionWorkerService worker tasks.</summary>
    public ChannelReader<ExtractionJob> Reader => _channel.Reader;
}

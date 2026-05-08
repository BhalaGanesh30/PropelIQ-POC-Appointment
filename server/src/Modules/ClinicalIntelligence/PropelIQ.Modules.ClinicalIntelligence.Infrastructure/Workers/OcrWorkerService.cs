using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Workers;

/// <summary>
/// Background service that drains <see cref="OcrJobChannel"/> using
/// <see cref="OcrConfiguration.ConcurrencyLimit"/> concurrent worker tasks.
///
/// Per-job lifecycle:
/// 1. Set <c>extraction_status = Processing</c>.
/// 2. Invoke <see cref="IOcrProcessingService.ProcessDocumentAsync"/>.
/// 3. On success: set <c>extraction_status = Completed</c>, store text, set manual-review flag.
/// 4. On failure: increment <c>RetryCount</c>.
///    - If retries remain: re-enqueue after exponential backoff (1s, 4s, 16s).
///    - If retries exhausted: set <c>extraction_status = Failed</c>, write dead-letter entry (AC-4).
///
/// Scoped services are resolved per job via <see cref="IServiceScopeFactory"/> (TR-005).
/// OpenTelemetry metrics are emitted for processed jobs, failures, and duration (NFR-011).
/// </summary>
public sealed class OcrWorkerService : BackgroundService
{
    // ── Metrics ──────────────────────────────────────────────────────────────
    private static readonly Counter<long> _processedCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "ocr.jobs.processed",
            unit: "{jobs}",
            description: "Total OCR jobs processed successfully.");

    private static readonly Counter<long> _failedCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "ocr.jobs.failed",
            unit: "{jobs}",
            description: "Total OCR jobs that moved to the dead-letter queue.");

    private static readonly Histogram<double> _durationHistogram =
        DiagnosticsConfig.Meter.CreateHistogram<double>(
            "ocr.processing.duration_ms",
            unit: "ms",
            description: "OCR job processing duration in milliseconds.");

    // ─────────────────────────────────────────────────────────────────────────

    private readonly OcrJobChannel _channel;
    private readonly ExtractionJobChannel _extractionChannel;
    private readonly OcrConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OcrWorkerService> _logger;

    public OcrWorkerService(
        OcrJobChannel channel,
        ExtractionJobChannel extractionChannel,
        IOptions<OcrConfiguration> options,
        IServiceScopeFactory scopeFactory,
        ILogger<OcrWorkerService> logger)
    {
        _channel           = channel;
        _extractionChannel = extractionChannel;
        _config            = options.Value;
        _scopeFactory      = scopeFactory;
        _logger            = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OcrWorkerService started with {ConcurrencyLimit} worker(s).",
            _config.ConcurrencyLimit);

        var workerTasks = Enumerable
            .Range(0, _config.ConcurrencyLimit)
            .Select(i => RunWorkerAsync(i, stoppingToken))
            .ToArray();

        await Task.WhenAll(workerTasks);

        _logger.LogInformation("OcrWorkerService stopped.");
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken ct)
    {
        _logger.LogDebug("OCR worker {WorkerId} started.", workerId);

        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            await ProcessJobAsync(job, ct);
        }

        _logger.LogDebug("OCR worker {WorkerId} stopped.", workerId);
    }

    private async Task ProcessJobAsync(OcrJob job, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository    = scope.ServiceProvider.GetRequiredService<IClinicalDocumentRepository>();
            var ocrService    = scope.ServiceProvider.GetRequiredService<IOcrProcessingService>();
            var deadLetterRepo = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

            // AC-2 step 1: transition to Processing
            await repository.UpdateExtractionStatusAsync(
                job.DocumentId,
                ExtractionStatus.Processing,
                ct);

            OcrProcessingResult result;

            try
            {
                result = await ocrService.ProcessDocumentAsync(job, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await HandleFailureAsync(job, ex, repository, deadLetterRepo, ct);
                return;
            }

            sw.Stop();

            // AC-2 step 2: persist result and mark Completed
            var document = await repository.GetByIdAsync(job.DocumentId, ct);
            if (document is not null)
            {
                document.ExtractedText     = result.ExtractedText;
                document.NeedsManualReview = result.NeedsManualReview;
                document.ExtractionStatus  = ExtractionStatus.Completed.ToString();
                await repository.UpdateAsync(document, ct);
            }

            _processedCounter.Add(1, new KeyValuePair<string, object?>("document_id", job.DocumentId));
            _durationHistogram.Record(sw.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "OCR completed for document {DocumentId}. " +
                "Confidence={Confidence:P1} NeedsReview={NeedsReview} Duration={Duration}ms.",
                job.DocumentId, result.AverageConfidence, result.NeedsManualReview,
                (int)sw.Elapsed.TotalMilliseconds);

            // Enqueue extraction job if text was produced
            if (!string.IsNullOrWhiteSpace(result.ExtractedText) && document is not null)
            {
                await _extractionChannel.Writer.WriteAsync(
                    new ExtractionJob(
                        DocumentId:    job.DocumentId,
                        PatientId:     document.PatientId,
                        ExtractedText: result.ExtractedText),
                    ct);

                _logger.LogDebug(
                    "Extraction job enqueued for document {DocumentId}.",
                    job.DocumentId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "OCR worker encountered an unhandled error for document {DocumentId}.",
                job.DocumentId);
        }
    }

    private async Task HandleFailureAsync(
        OcrJob job,
        Exception ex,
        IClinicalDocumentRepository repository,
        IDeadLetterRepository deadLetterRepo,
        CancellationToken ct)
    {
        var nextRetryCount = job.RetryCount + 1;

        if (nextRetryCount <= _config.MaxRetries)
        {
            // Exponential backoff: base^(2 * retryIndex) → 1s, 4s, 16s
            var delaySeconds = Math.Pow(_config.BackoffBaseSeconds * 2, job.RetryCount);
            var delay = TimeSpan.FromSeconds(delaySeconds);

            _logger.LogWarning(
                "OCR failed for document {DocumentId} (attempt {Attempt}/{MaxRetries}). " +
                "Retrying in {DelaySeconds}s. Error: {Message}",
                job.DocumentId, nextRetryCount, _config.MaxRetries,
                (int)delaySeconds, ex.Message);

            // Reset to Queued before re-enqueue so polling callers see correct state
            await repository.UpdateExtractionStatusAsync(
                job.DocumentId,
                ExtractionStatus.Queued,
                ct);

            await Task.Delay(delay, ct);

            await _channel.Writer.WriteAsync(
                job with { RetryCount = nextRetryCount },
                ct);
        }
        else
        {
            // AC-4: retries exhausted — move to dead-letter queue
            _logger.LogError(ex,
                "OCR permanently failed for document {DocumentId} after {MaxRetries} retries.",
                job.DocumentId, _config.MaxRetries);

            await repository.UpdateExtractionStatusAsync(
                job.DocumentId,
                ExtractionStatus.Failed,
                ct);

            await deadLetterRepo.AddAsync(new DeadLetterEntry
            {
                DocumentId    = job.DocumentId,
                ErrorMessage  = ex.Message,
                StackTrace    = ex.StackTrace,
                RetryCount    = nextRetryCount,
            }, ct);

            _failedCounter.Add(1, new KeyValuePair<string, object?>("document_id", job.DocumentId));
        }
    }
}

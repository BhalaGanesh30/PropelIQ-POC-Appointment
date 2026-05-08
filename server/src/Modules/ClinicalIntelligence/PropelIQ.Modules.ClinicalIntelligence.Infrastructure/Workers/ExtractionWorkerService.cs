using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Workers;

/// <summary>
/// Background service that drains <see cref="ExtractionJobChannel"/> using
/// <see cref="ExtractionConfiguration.ConcurrencyLimit"/> concurrent worker tasks.
///
/// Per-job lifecycle:
/// 1. Call <see cref="IClinicalExtractionService.ExtractEntitiesAsync"/>.
/// 2. On success: update extraction_status = Completed in clinical_documents.
/// 3. On failure: retry with exponential backoff (1s, 4s, 16s).
/// 4. After max retries: set extraction_status = Failed, write dead-letter entry.
///
/// Scoped services are resolved per job via <see cref="IServiceScopeFactory"/>.
/// OpenTelemetry metrics are emitted for processed jobs, failures, and duration.
/// </summary>
public sealed class ExtractionWorkerService : BackgroundService
{
    private readonly ExtractionJobChannel _channel;
    private readonly ExtractionConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtractionWorkerService> _logger;

    public ExtractionWorkerService(
        ExtractionJobChannel channel,
        IOptions<ExtractionConfiguration> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ExtractionWorkerService> logger)
    {
        _channel      = channel;
        _config       = options.Value;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ExtractionWorkerService started with {ConcurrencyLimit} worker(s).",
            _config.ConcurrencyLimit);

        var workerTasks = Enumerable
            .Range(0, _config.ConcurrencyLimit)
            .Select(i => RunWorkerAsync(i, stoppingToken))
            .ToArray();

        await Task.WhenAll(workerTasks);

        _logger.LogInformation("ExtractionWorkerService stopped.");
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken ct)
    {
        _logger.LogDebug("Extraction worker {WorkerId} started.", workerId);

        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            await ProcessJobAsync(job, ct);
        }

        _logger.LogDebug("Extraction worker {WorkerId} stopped.", workerId);
    }

    private async Task ProcessJobAsync(ExtractionJob job, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int attempt = job.RetryCount + 1;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var extractionService = scope.ServiceProvider.GetRequiredService<IClinicalExtractionService>();
            var documentRepo      = scope.ServiceProvider.GetRequiredService<IClinicalDocumentRepository>();

            var result = await extractionService.ExtractEntitiesAsync(job, ct);

            // On success, update extraction_status = Completed
            var document = await documentRepo.GetByIdAsync(job.DocumentId, ct);
            if (document is not null)
            {
                document.ExtractionStatus = "Completed";
                await documentRepo.UpdateAsync(document, ct);
            }

            DiagnosticsConfig.Meter.CreateCounter<long>(
                "extraction.jobs.processed", unit: "{jobs}", description: "Extraction jobs processed successfully.")
                .Add(1);

            _logger.LogInformation(
                "Extraction completed for document {DocumentId} in {Duration}ms.",
                job.DocumentId, (int)sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Extraction failed for document {DocumentId} (attempt {Attempt}).", job.DocumentId, attempt);

            if (attempt < _config.MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(_config.BackoffBaseSeconds, attempt));
                _logger.LogWarning(
                    "Retrying extraction for document {DocumentId} after {Delay}s (attempt {Attempt}/{Max}).",
                    job.DocumentId, delay.TotalSeconds, attempt, _config.MaxRetries);

                await Task.Delay(delay, ct);
                await _channel.Writer.WriteAsync(job with { RetryCount = attempt }, ct);
            }
            else
            {
                // Mark as failed after max retries
                await using var scope = _scopeFactory.CreateAsyncScope();
                var documentRepo = scope.ServiceProvider.GetRequiredService<IClinicalDocumentRepository>();
                var document     = await documentRepo.GetByIdAsync(job.DocumentId, ct);
                if (document is not null)
                {
                    document.ExtractionStatus = "Failed";
                    await documentRepo.UpdateAsync(document, ct);
                }
                DiagnosticsConfig.Meter.CreateCounter<long>(
                    "extraction.jobs.failed", unit: "{jobs}", description: "Extraction jobs moved to dead-letter queue.")
                    .Add(1);
            }
        }
    }
}

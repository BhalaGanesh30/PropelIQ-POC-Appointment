using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using Tesseract;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Tesseract.NET SDK implementation of <see cref="IOcrProcessingService"/>.
///
/// Processing steps:
/// 1. Download file bytes from Cloudflare R2 via <see cref="IR2DocumentStorageService"/>.
/// 2. Load image into Tesseract using <c>Pix.LoadFromMemory</c>.
/// 3. Run OCR with the configured tessdata path and language "eng".
/// 4. Collect per-word confidence scores and compute the average.
/// 5. Set <c>NeedsManualReview = true</c> when average confidence is below
///    <see cref="OcrConfiguration.ConfidenceThreshold"/> (Edge Case 1).
/// </summary>
public sealed class TesseractOcrService : IOcrProcessingService
{
    private readonly IR2DocumentStorageService _storage;
    private readonly OcrConfiguration _config;
    private readonly ILogger<TesseractOcrService> _logger;

    public TesseractOcrService(
        IR2DocumentStorageService storage,
        IOptions<OcrConfiguration> options,
        ILogger<TesseractOcrService> logger)
    {
        _storage = storage;
        _config  = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task<OcrProcessingResult> ProcessDocumentAsync(OcrJob job, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "OCR: Downloading document {DocumentId} from R2 key '{R2ObjectKey}'.",
            job.DocumentId, job.R2ObjectKey);

        await using var downloadStream = await _storage.DownloadAsync(job.R2ObjectKey, ct);

        // Buffer the stream — Tesseract.NET requires a seekable byte array
        using var ms = new MemoryStream();
        await downloadStream.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        _logger.LogDebug(
            "OCR: Running Tesseract on document {DocumentId} ({Bytes} bytes).",
            job.DocumentId, fileBytes.Length);

        using var engine = new TesseractEngine(_config.TessdataPath, "eng", EngineMode.Default);
        using var pix    = Pix.LoadFromMemory(fileBytes);
        using var page   = engine.Process(pix);

        var extractedText = page.GetText();

        var confidenceValues = CollectWordConfidences(page);
        var averageConfidence = confidenceValues.Count > 0
            ? confidenceValues.Average()
            : 0.0;

        var needsManualReview = averageConfidence < _config.ConfidenceThreshold;

        if (needsManualReview)
        {
            _logger.LogWarning(
                "OCR: Document {DocumentId} flagged for manual review. " +
                "AverageConfidence={AverageConfidence:P1} Threshold={Threshold:P1}.",
                job.DocumentId, averageConfidence, _config.ConfidenceThreshold);
        }

        return new OcrProcessingResult(
            ExtractedText:      extractedText ?? string.Empty,
            AverageConfidence:  averageConfidence,
            NeedsManualReview:  needsManualReview);
    }

    /// <summary>
    /// Iterates all recognized words on the page and returns their individual confidence
    /// scores normalised to the 0.0–1.0 range.
    /// </summary>
    private static List<double> CollectWordConfidences(Page page)
    {
        var confidences = new List<double>();

        using var iter = page.GetIterator();
        iter.Begin();

        do
        {
            if (iter.IsAtBeginningOf(PageIteratorLevel.Word))
            {
                // Tesseract returns confidence as 0–100; normalise to 0.0–1.0.
                confidences.Add(iter.GetConfidence(PageIteratorLevel.Word) / 100.0);
            }
        }
        while (iter.Next(PageIteratorLevel.Word));

        return confidences;
    }
}

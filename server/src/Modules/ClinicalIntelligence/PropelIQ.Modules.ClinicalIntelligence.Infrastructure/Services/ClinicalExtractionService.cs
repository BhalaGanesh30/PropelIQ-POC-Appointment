using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Configuration;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;
using PropelIQ.SharedKernel.Observability;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Orchestrates the full clinical entity extraction pipeline for a single document:
///
/// 1. Quality gate — reject text below <see cref="ExtractionConfiguration.LowQualityTextLengthThreshold"/>.
/// 2. Chunking — split on sentence boundaries up to <see cref="ExtractionConfiguration.MaxChunkSize"/> chars.
/// 3. For each chunk: PII redact → build prompt → call AI gateway → validate schema.
/// 4. Aggregate and deduplicate facts across chunks.
/// 5. Normalize entity names with deterministic rules (AIR-001 hybrid).
/// 6. Flag low-confidence facts <c>NeedsReview = true</c> (AC-3, AIR-005).
/// 7. Persist facts via <see cref="IClinicalFactRepository"/> with <c>DocumentId</c> (AIR-004, AC-2).
///
/// Fallback: when the AI gateway is unavailable (circuit breaker open), the pipeline
/// returns empty results and marks the document for manual review (TR-008, AIR-005).
/// </summary>
public sealed class ClinicalExtractionService : IClinicalExtractionService
{
    // ── Metrics ──────────────────────────────────────────────────────────────
    private static readonly Counter<long> _extractedFactsCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "extraction.facts.extracted",
            unit: "{facts}",
            description: "Total clinical facts extracted and persisted.");

    private static readonly Counter<long> _lowQualityCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "extraction.jobs.low_quality",
            unit: "{jobs}",
            description: "Extraction jobs skipped due to low input text quality (Edge Case 1).");

    private static readonly Counter<long> _gatewayFallbackCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>(
            "extraction.gateway.fallback",
            unit: "{events}",
            description: "AI gateway circuit-breaker fallback activations (TR-008).");

    // ─────────────────────────────────────────────────────────────────────────

    private readonly IAiGatewayClient          _aiGateway;
    private readonly IPiiRedactionService      _piiRedaction;
    private readonly IPromptBuilder            _promptBuilder;
    private readonly IExtractionSchemaValidator _schemaValidator;
    private readonly INormalizationService     _normalization;
    private readonly IClinicalFactRepository   _factRepository;
    private readonly IClinicalDocumentRepository _documentRepository;
    private readonly ExtractionConfiguration   _config;
    private readonly ILogger<ClinicalExtractionService> _logger;

    public ClinicalExtractionService(
        IAiGatewayClient aiGateway,
        IPiiRedactionService piiRedaction,
        IPromptBuilder promptBuilder,
        IExtractionSchemaValidator schemaValidator,
        INormalizationService normalization,
        IClinicalFactRepository factRepository,
        IClinicalDocumentRepository documentRepository,
        IOptions<ExtractionConfiguration> config,
        ILogger<ClinicalExtractionService> logger)
    {
        _aiGateway          = aiGateway;
        _piiRedaction       = piiRedaction;
        _promptBuilder      = promptBuilder;
        _schemaValidator    = schemaValidator;
        _normalization      = normalization;
        _factRepository     = factRepository;
        _documentRepository = documentRepository;
        _config             = config.Value;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<ExtractionResult> ExtractEntitiesAsync(
        ExtractionJob job,
        CancellationToken ct = default)
    {
        using var activity = DiagnosticsConfig.ActivitySource.StartActivity(
            "ClinicalExtraction.Extract",
            ActivityKind.Internal);

        activity?.SetTag("extraction.document_id", job.DocumentId);
        activity?.SetTag("extraction.patient_id",  job.PatientId);

        // ── Step 1: quality gate ──────────────────────────────────────────────
        if (job.ExtractedText.Length < _config.LowQualityTextLengthThreshold)
        {
            _logger.LogWarning(
                "Document {DocumentId} text length {Length} is below threshold {Threshold}. " +
                "Marking for manual review (Edge Case 1).",
                job.DocumentId, job.ExtractedText.Length, _config.LowQualityTextLengthThreshold);

            await FlagDocumentForManualReviewAsync(job.DocumentId, ct);
            _lowQualityCounter.Add(1);

            return new ExtractionResult([], LowInputQuality: true, 0, 0);
        }

        // ── Step 2: fast-exit if circuit breaker is open ──────────────────────
        if (_aiGateway.IsCircuitBreakerOpen)
        {
            _logger.LogWarning(
                "AI gateway circuit breaker is open for document {DocumentId}. " +
                "Falling back to manual review (TR-008, AIR-005).",
                job.DocumentId);

            await FlagDocumentForManualReviewAsync(job.DocumentId, ct);
            _gatewayFallbackCounter.Add(1);

            return new ExtractionResult([], LowInputQuality: false, 0, 0);
        }

        // ── Step 3: chunk → redact → prompt → validate ────────────────────────
        var chunks         = ChunkText(job.ExtractedText);
        var rawFacts       = new List<ExtractedFact>();
        int schemaTotal    = 0;
        int schemaPass     = 0;
        bool gatewayFailed = false;

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();

            var redaction = _piiRedaction.Redact(chunk);

            if (redaction.RedactionActions.Count > 0)
            {
                _logger.LogDebug(
                    "PII redacted {Count} item(s) from chunk (AIR-009). DocumentId={DocumentId}.",
                    redaction.RedactionActions.Count, job.DocumentId);
            }

            var messages = _promptBuilder.BuildExtractionMessages(
                redaction.RedactedText,
                _config.ExtractionModelId,
                _config.MaxTokens);

            var request = new ChatCompletionRequest
            {
                Model       = _config.ExtractionModelId,
                Messages    = messages,
                Temperature = 0.0,    // deterministic extraction — no creativity wanted
                MaxTokens   = _config.MaxTokens,
            };

            ChatCompletionResponse? response;
            try
            {
                response = await _aiGateway.GetCompletionAsync(request, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "AI gateway call failed for document {DocumentId}. Skipping chunk (AIR-005).",
                    job.DocumentId);
                gatewayFailed = true;
                continue;
            }

            if (response is null)
            {
                // Circuit breaker opened mid-pipeline — stop processing.
                _logger.LogWarning(
                    "AI gateway returned null for document {DocumentId} (circuit breaker open). " +
                    "Activating fallback (TR-008).",
                    job.DocumentId);

                gatewayFailed = true;
                _gatewayFallbackCounter.Add(1);
                break;
            }

            var rawJson = response.Choices.FirstOrDefault()?.Message.Content;
            schemaTotal++;

            var chunkFacts = _schemaValidator.Validate(rawJson ?? string.Empty);
            if (chunkFacts is not null)
            {
                schemaPass++;
                rawFacts.AddRange(chunkFacts);
            }
        }

        // If every chunk failed the gateway, flag for manual review.
        if (gatewayFailed && rawFacts.Count == 0)
        {
            await FlagDocumentForManualReviewAsync(job.DocumentId, ct);
            return new ExtractionResult([], LowInputQuality: false, schemaPass, schemaTotal);
        }

        // ── Step 4: deduplicate ───────────────────────────────────────────────
        var deduplicated = rawFacts
            .GroupBy(f => (f.FactType.ToLowerInvariant(), f.Name.ToLowerInvariant(), f.Value.ToLowerInvariant()))
            .Select(g => g.OrderByDescending(f => f.Confidence).First())
            .ToList();

        // ── Step 5: normalize ─────────────────────────────────────────────────
        var normalized = _normalization.Normalize(deduplicated);

        // ── Step 6: flag low-confidence facts ─────────────────────────────────
        var entities = normalized
            .Select(f => new ClinicalFact
            {
                DocumentId      = job.DocumentId,
                PatientId       = job.PatientId,
                FactType        = f.FactType,
                Name            = f.Name,
                Value           = f.Value,
                ConfidenceScore = f.Confidence,
                NeedsReview     = f.Confidence < _config.ConfidenceThreshold,  // AC-3, AIR-005
                SourceText      = f.SourceText,                                 // AIR-004
            })
            .ToList();

        // ── Step 7: persist ───────────────────────────────────────────────────
        if (entities.Count > 0)
        {
            await _factRepository.AddRangeAsync(entities, ct);
            _extractedFactsCounter.Add(entities.Count,
                new KeyValuePair<string, object?>("document_id", job.DocumentId));
        }

        var lowConfidenceCount = entities.Count(e => e.NeedsReview);
        _logger.LogInformation(
            "Extraction complete for document {DocumentId}: {Total} facts, " +
            "{LowConf} low-confidence, schema pass {Pass}/{Total} (AC-3, AC-4).",
            job.DocumentId, entities.Count, lowConfidenceCount, schemaPass, schemaTotal);

        return new ExtractionResult(normalized, LowInputQuality: false, schemaPass, schemaTotal);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerable<string> ChunkText(string text)
    {
        if (text.Length <= _config.MaxChunkSize)
        {
            yield return text;
            yield break;
        }

        // Sentence-boundary-aware chunking: split on '. ', '? ', '! ' or '\n'
        // and accumulate until the chunk would exceed MaxChunkSize.
        var separators = new[] { ". ", "? ", "! ", "\n" };
        var sentences  = SplitSentences(text, separators);
        var buffer     = new System.Text.StringBuilder();

        foreach (var sentence in sentences)
        {
            if (buffer.Length + sentence.Length > _config.MaxChunkSize && buffer.Length > 0)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();
            }

            buffer.Append(sentence);
        }

        if (buffer.Length > 0)
            yield return buffer.ToString().Trim();
    }

    private static IEnumerable<string> SplitSentences(string text, string[] separators)
    {
        int start = 0;
        while (start < text.Length)
        {
            int idx = -1;
            string? foundSep = null;

            foreach (var sep in separators)
            {
                int pos = text.IndexOf(sep, start, StringComparison.Ordinal);
                if (pos >= 0 && (idx < 0 || pos < idx))
                {
                    idx      = pos;
                    foundSep = sep;
                }
            }

            if (idx < 0)
            {
                yield return text[start..];
                yield break;
            }

            yield return text[start..(idx + foundSep!.Length)];
            start = idx + foundSep.Length;
        }
    }

    private async Task FlagDocumentForManualReviewAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await _documentRepository.GetByIdAsync(documentId, ct);
        if (doc is not null)
        {
            doc.NeedsManualReview = true;
            await _documentRepository.UpdateAsync(doc, ct);
        }
    }
}

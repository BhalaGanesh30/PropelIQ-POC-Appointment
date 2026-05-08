using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Serves document content and full-text search for the in-browser document viewer (US_042).
///
/// Content endpoint: generates a 15-minute pre-signed Cloudflare R2 URL so the browser
/// loads the document directly from object storage (AC-1, Edge Case 2 range-request support).
///
/// Search endpoint: uses PostgreSQL FTS (GIN index on <c>extracted_text</c>) for the initial
/// match filter, then extracts individual occurrence positions and context in-memory (AC-4).
/// Returns extraction status so the frontend can disable search while OCR is in progress (Edge Case 1).
/// </summary>
public sealed class DocumentViewerService : IDocumentViewerService
{
    private static readonly TimeSpan PreSignedUrlExpiry = TimeSpan.FromMinutes(15);
    private const int ContextWindowChars = 50;
    private const int MaxMatchesReturned = 50;

    private readonly IClinicalDocumentRepository _repository;
    private readonly IR2DocumentStorageService _storage;
    private readonly ILogger<DocumentViewerService> _logger;

    public DocumentViewerService(
        IClinicalDocumentRepository repository,
        IR2DocumentStorageService storage,
        ILogger<DocumentViewerService> logger)
    {
        _repository = repository;
        _storage    = storage;
        _logger     = logger;
    }

    /// <inheritdoc />
    public async Task<DocumentContentResponse?> GetDocumentContentAsync(
        Guid documentId,
        CancellationToken ct = default)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);

        if (document is null)
            return null;

        // Only serve documents that have passed malware scan (security boundary)
        if (document.ScanResult != "Clean")
        {
            _logger.LogWarning(
                "Content request for document {DocumentId} denied — ScanResult={ScanResult}.",
                documentId, document.ScanResult);
            return null;
        }

        if (string.IsNullOrEmpty(document.R2ObjectKey))
            return null;

        var preSignedUrl = await _storage.GeneratePreSignedUrlAsync(
            document.R2ObjectKey,
            PreSignedUrlExpiry,
            ct);

        _logger.LogDebug(
            "Generated pre-signed URL for document {DocumentId} (expires in {ExpiryMinutes} min).",
            documentId, (int)PreSignedUrlExpiry.TotalMinutes);

        return new DocumentContentResponse
        {
            PreSignedUrl      = preSignedUrl,
            ContentType       = document.ContentType,
            ExtractionStatus  = document.ExtractionStatus,
            OriginalFilename  = document.FileName,
        };
    }

    /// <inheritdoc />
    public async Task<DocumentSearchResponse?> SearchDocumentAsync(
        Guid documentId,
        string searchTerm,
        CancellationToken ct = default)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);

        if (document is null)
            return null;

        // Edge Case 1: OCR not yet complete — return status so frontend can disable search
        if (document.ExtractionStatus != ExtractionStatus.Completed.ToString())
        {
            return new DocumentSearchResponse
            {
                Matches          = [],
                TotalCount       = 0,
                ExtractionStatus = document.ExtractionStatus,
            };
        }

        // Fetch extracted text via FTS-filtered repository query (uses GIN index)
        var extractedText = await _repository.SearchExtractedTextAsync(documentId, searchTerm, ct);

        if (string.IsNullOrEmpty(extractedText))
        {
            return new DocumentSearchResponse
            {
                Matches          = [],
                TotalCount       = 0,
                ExtractionStatus = document.ExtractionStatus,
            };
        }

        var matches = FindMatches(extractedText, searchTerm);

        return new DocumentSearchResponse
        {
            Matches          = matches.Take(MaxMatchesReturned).ToList(),
            TotalCount       = matches.Count,
            ExtractionStatus = document.ExtractionStatus,
        };
    }

    /// <summary>
    /// Finds all case-insensitive occurrences of <paramref name="searchTerm"/> in
    /// <paramref name="text"/> and returns a <see cref="SearchMatchDto"/> for each.
    /// </summary>
    private static List<SearchMatchDto> FindMatches(string text, string searchTerm)
    {
        var results = new List<SearchMatchDto>();

        // Escape term for regex; we want literal string matching (not a pattern search)
        var escapedTerm = Regex.Escape(searchTerm);
        var regex = new Regex(escapedTerm, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (Match match in regex.Matches(text))
        {
            var startIndex = match.Index;
            var endIndex   = startIndex + match.Length;

            var contextBeforeStart = Math.Max(0, startIndex - ContextWindowChars);
            var contextBefore = text.Substring(contextBeforeStart, startIndex - contextBeforeStart);

            var contextAfterEnd = Math.Min(text.Length, endIndex + ContextWindowChars);
            var contextAfter = text.Substring(endIndex, contextAfterEnd - endIndex);

            results.Add(new SearchMatchDto
            {
                Text          = match.Value,
                Position      = startIndex,
                PageNumber    = null, // page info not available from plain-text OCR
                ContextBefore = contextBefore,
                ContextAfter  = contextAfter,
            });
        }

        return results;
    }
}

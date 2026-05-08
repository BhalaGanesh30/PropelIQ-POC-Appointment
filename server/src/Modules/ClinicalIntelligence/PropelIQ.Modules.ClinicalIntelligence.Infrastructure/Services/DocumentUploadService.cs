using Microsoft.Extensions.Logging;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Validators;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Orchestrates the document upload pipeline:
/// (1) magic-byte file type validation (AC-1, AC-4)
/// (2) file size check (AC-1, Edge Case 2)
/// (3) ClamAV malware scan (AC-2, AC-3, Edge Case 1)
/// (4) Cloudflare R2 storage with SSE-S3 encryption (NFR-007)
/// (5) <c>clinical_documents</c> record persistence
/// (6) OCR job enqueue for clean files (US_041 AC-1)
/// </summary>
public sealed class DocumentUploadService : IDocumentUploadService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const string DocumentsPrefix  = "documents";
    private const string QuarantinePrefix = "quarantine";

    private readonly IMalwareScanService _scanner;
    private readonly IR2DocumentStorageService _storage;
    private readonly IClinicalDocumentRepository _repository;
    private readonly OcrJobChannel _ocrChannel;
    private readonly ILogger<DocumentUploadService> _logger;

    public DocumentUploadService(
        IMalwareScanService scanner,
        IR2DocumentStorageService storage,
        IClinicalDocumentRepository repository,
        OcrJobChannel ocrChannel,
        ILogger<DocumentUploadService> logger)
    {
        _scanner    = scanner;
        _storage    = storage;
        _repository = repository;
        _ocrChannel = ocrChannel;
        _logger     = logger;
    }

    public async Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadCommand command, CancellationToken ct = default)
    {
        // AC-1 / AC-4 — magic-byte file type validation
        if (!FileTypeValidator.IsAllowedFileType(command.FileContent))
        {
            throw new InvalidOperationException(
                "Invalid file type. Accepted formats: PDF, JPG, PNG, TIFF.");
        }

        // Edge Case 2 — size check (belt-and-suspenders alongside [RequestSizeLimit])
        if (command.FileSizeBytes > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                "File exceeds the maximum allowed size of 10 MB.");
        }

        // AC-2 — malware scan must complete before persistence
        var scanResult = await _scanner.ScanAsync(command.FileContent, ct);

        // AC-3 — reject confirmed threats without any persistence
        if (scanResult == ScanResult.ThreatDetected)
        {
            _logger.LogWarning(
                "SECURITY EVENT: Malware rejected. File={FileName} Patient={PatientId}",
                command.FileName, command.PatientId);

            throw new InvalidOperationException(
                "The uploaded file was rejected: malware detected.");
        }

        bool isPendingScan = scanResult == ScanResult.ScannerUnavailable;
        var effectiveScanResult = isPendingScan ? ScanResult.PendingScan : ScanResult.Clean;

        // Persist first to get the DB-generated Id, then upload with that Id in the key.
        var entity = new ClinicalDocument
        {
            PatientId        = command.PatientId,
            FileName         = command.FileName,
            ContentType      = command.ContentType,
            FileSizeBytes    = command.FileSizeBytes,
            ScanResult       = effectiveScanResult.ToString(),
            ExtractionStatus = ExtractionStatus.Queued.ToString(),
        };

        // Determine R2 prefix: quarantine for pending-scan, documents/ for clean
        var prefix = isPendingScan ? QuarantinePrefix : DocumentsPrefix;
        var objectKey = $"{prefix}/{command.PatientId}/{entity.Id}";
        entity.R2ObjectKey = objectKey;

        command.FileContent.Seek(0, SeekOrigin.Begin);
        await _storage.UploadAsync(command.FileContent, objectKey, command.ContentType, ct);

        await _repository.AddAsync(entity, ct);

        // AC-1 (US_041): Enqueue OCR job only for clean files that are stored in
        // the documents/ prefix. Quarantined (PendingScan) files are not OCR-eligible
        // until the malware scan retry service moves them to documents/.
        if (!isPendingScan)
        {
            await _ocrChannel.Writer.WriteAsync(
                new OcrJob(entity.Id, objectKey),
                ct);

            _logger.LogDebug(
                "OCR job enqueued for document {DocumentId} key='{Key}'.",
                entity.Id, objectKey);
        }

        var message = isPendingScan
            ? "File uploaded and queued for malware scanning. It will be available once the scan completes."
            : "File uploaded successfully.";

        return new DocumentUploadResponse
        {
            DocumentId = entity.Id,
            ScanResult = effectiveScanResult.ToString(),
            Message    = message,
        };
    }

    public async Task<DocumentStatusResponse?> GetDocumentStatusAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _repository.GetByIdAsync(documentId, ct);
        if (document is null)
            return null;

        return new DocumentStatusResponse
        {
            DocumentId       = document.Id,
            ScanResult       = document.ScanResult,
            ExtractionStatus = document.ExtractionStatus,
            Message          = BuildStatusMessage(document.ScanResult),
        };
    }

    private static string BuildStatusMessage(string scanResult) => scanResult switch
    {
        "Clean"           => "File is clean and available.",
        "PendingScan"     => "File is awaiting malware scan.",
        "ThreatDetected"  => "File was rejected due to detected malware.",
        _                 => "Status unknown.",
    };
}

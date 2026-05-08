using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropelIQ.Api.Controllers;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.ClinicalIntelligence.Application.Dto;
using PropelIQ.Modules.ClinicalIntelligence.Application.Enums;
using PropelIQ.Modules.ClinicalIntelligence.Application.Models;
using PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Queues;
using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Api.Controllers;

/// <summary>
/// Handles clinical document upload, status polling, OCR retry, content serving,
/// and full-text search (US_040, US_041, US_042).
/// Route: <c>api/v1/documents</c> (from <see cref="BaseApiController"/>).
/// Authorization is declared per endpoint to allow role variations.
/// </summary>
[Authorize]
public sealed class DocumentsController : BaseApiController
{
    private readonly IDocumentUploadService _uploadService;
    private readonly IClinicalDocumentRepository _documentRepository;
    private readonly OcrJobChannel _ocrChannel;
    private readonly IDocumentViewerService _viewerService;

    public DocumentsController(
        IDocumentUploadService uploadService,
        IClinicalDocumentRepository documentRepository,
        OcrJobChannel ocrChannel,
        IDocumentViewerService viewerService)
    {
        _uploadService      = uploadService;
        _documentRepository = documentRepository;
        _ocrChannel         = ocrChannel;
        _viewerService      = viewerService;
    }

    /// <summary>
    /// Uploads a clinical document for a patient.
    ///
    /// Validates file type via magic-byte inspection (AC-1, AC-4) and enforces
    /// a 10 MB size limit (AC-1, Edge Case 2).  A ClamAV malware scan is performed
    /// before the file is stored in Cloudflare R2 (AC-2, AC-3).
    ///
    /// When the scanner is unavailable, the file is stored in a quarantine prefix
    /// with <c>ScanResult = "PendingScan"</c> and will be re-scanned by a background
    /// worker (Edge Case 1).
    /// </summary>
    /// <param name="file">The file to upload (PDF, JPG, PNG, TIFF; max 10 MB).</param>
    /// <param name="patientId">UUID of the patient the document belongs to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Document accepted; <c>ScanResult</c> indicates outcome.</response>
    /// <response code="400">File type, size validation failed, or malware detected.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have the Patient or Staff role.</response>
    [HttpPost("upload")]
    [Authorize(Roles = "Patient,Staff")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upload(
        [Required] IFormFile file,
        [Required] Guid patientId,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        var command = new DocumentUploadCommand(
            FileContent:   file.OpenReadStream(),
            FileName:      file.FileName,
            ContentType:   file.ContentType,
            FileSizeBytes: file.Length,
            PatientId:     patientId);

        try
        {
            var response = await _uploadService.UploadDocumentAsync(command, ct);

            // 201 for accepted uploads; scanner-unavailable also returns 201 with PendingScan
            return CreatedAtAction(
                nameof(GetStatus),
                new { id = response.DocumentId },
                response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns the current malware scan result and extraction status for a document.
    /// Used by the frontend to poll for completion after upload (US_040 task_001).
    /// </summary>
    /// <param name="id">UUID of the document to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Status returned.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have the Patient or Staff role.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{id:guid}/status")]
    [Authorize(Roles = "Patient,Staff")]
    [ProducesResponseType(typeof(DocumentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var status = await _uploadService.GetDocumentStatusAsync(id, ct);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>
    /// Manually re-triggers OCR processing for a document whose extraction has failed.
    ///
    /// Validates that the document exists and is in the <c>Failed</c> state, resets its
    /// <c>extraction_status</c> back to <c>Queued</c>, and re-enqueues an <see cref="OcrJob"/>
    /// with <c>RetryCount = 0</c> for fresh processing (US_041 AC-4).
    /// </summary>
    /// <param name="id">UUID of the document to retry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">OCR job re-queued successfully.</response>
    /// <response code="400">Document is not in the <c>Failed</c> state.</response>
    /// <response code="401">JWT bearer token missing or invalid.</response>
    /// <response code="403">Caller does not have the Clinician or Staff role.</response>
    /// <response code="404">Document not found.</response>
    [HttpPost("{id:guid}/retry-ocr")]
    [Authorize(Roles = "Clinician,Staff")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryOcr(Guid id, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(id, ct);
        if (document is null)
            return NotFound();

        if (document.ExtractionStatus != ExtractionStatus.Failed.ToString())
            return BadRequest(new { error = $"Document is not in the Failed state (current: {document.ExtractionStatus})." });

        await _documentRepository.UpdateExtractionStatusAsync(id, ExtractionStatus.Queued, ct);

        await _ocrChannel.Writer.WriteAsync(
            new OcrJob(id, document.R2ObjectKey!),
            ct);

        return Accepted();
    }

    /// <summary>
    /// Returns a short-lived pre-signed Cloudflare R2 URL so the browser can load
    /// the document directly from object storage within the 3-second render target (AC-1).
    /// The URL supports HTTP range requests for progressive loading of large documents (Edge Case 2).
    /// </summary>
    /// <param name="id">UUID of the document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Pre-signed URL and document metadata returned.</response>
    /// <response code="403">Caller does not own this document (patient-scoped access).</response>
    /// <response code="404">Document not found or has not passed malware scan.</response>
    [HttpGet("{id:guid}/content")]
    [Authorize(Roles = "Patient,Staff,Clinician")]
    [ProducesResponseType(typeof(DocumentContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken ct)
    {
        var content = await _viewerService.GetDocumentContentAsync(id, ct);
        if (content is null)
            return NotFound();

        // Patient-scoped access control: verify the requesting patient owns the document
        if (User.IsInRole("Patient"))
        {
            var document = await _documentRepository.GetByIdAsync(id, ct);
            var userId = TryGetCurrentUserId();
            if (document is not null && userId.HasValue && document.PatientId != userId.Value)
                return Forbid();
        }

        return Ok(content);
    }

    /// <summary>
    /// Full-text searches the OCR-extracted text of a document and returns matched
    /// snippets with surrounding context (AC-4).  Returns the current
    /// <c>extractionStatus</c> so the frontend can disable search when OCR is still
    /// running (Edge Case 1).
    /// </summary>
    /// <param name="id">UUID of the document.</param>
    /// <param name="term">Search term (minimum 2 characters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Search results (may be empty) with extraction status.</response>
    /// <response code="400">Search term is missing or fewer than 2 characters.</response>
    /// <response code="403">Caller does not own this document.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{id:guid}/search")]
    [Authorize(Roles = "Patient,Staff,Clinician")]
    [ProducesResponseType(typeof(DocumentSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Search(
        Guid id,
        [FromQuery] string? term,
        CancellationToken ct)
    {
        // Sanitize and validate search term (minimum 2 chars prevents FTS abuse)
        var sanitizedTerm = term?.Trim();
        if (string.IsNullOrEmpty(sanitizedTerm) || sanitizedTerm.Length < 2)
            return BadRequest(new { error = "Search term must be at least 2 characters." });

        // Patient-scoped access control
        if (User.IsInRole("Patient"))
        {
            var document = await _documentRepository.GetByIdAsync(id, ct);
            if (document is null)
                return NotFound();
            var userId = TryGetCurrentUserId();
            if (userId.HasValue && document.PatientId != userId.Value)
                return Forbid();
        }

        var result = await _viewerService.SearchDocumentAsync(id, sanitizedTerm, ct);
        return result is null ? NotFound() : Ok(result);
    }
}

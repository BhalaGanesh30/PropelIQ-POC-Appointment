namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// Returned by <c>GET /api/v1/documents/{id}/content</c>.
/// Contains a short-lived pre-signed Cloudflare R2 URL the browser uses to load
/// the document directly from object storage, avoiding API server streaming (AC-1).
/// </summary>
public sealed class DocumentContentResponse
{
    /// <summary>Pre-signed R2 URL valid for 15 minutes (Edge Case 2: supports range requests).</summary>
    public string PreSignedUrl { get; set; } = string.Empty;

    /// <summary>MIME content-type (e.g. "application/pdf", "image/jpeg").</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Current OCR extraction state — used by frontend to enable/disable search (Edge Case 1).</summary>
    public string ExtractionStatus { get; set; } = string.Empty;

    /// <summary>Original filename as uploaded by the user.</summary>
    public string OriginalFilename { get; set; } = string.Empty;
}

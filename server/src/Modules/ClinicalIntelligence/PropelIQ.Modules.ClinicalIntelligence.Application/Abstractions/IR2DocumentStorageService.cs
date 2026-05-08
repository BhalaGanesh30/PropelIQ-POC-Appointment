namespace PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;

/// <summary>
/// R2-compatible object storage service for clinical documents.
/// Reuses the Cloudflare R2 S3-compatible API patterns established in US_038.
/// </summary>
public interface IR2DocumentStorageService
{
    /// <summary>
    /// Uploads <paramref name="stream"/> to R2 under the given <paramref name="key"/>
    /// with SSE-S3 server-side encryption (NFR-007).
    /// </summary>
    Task UploadAsync(Stream stream, string key, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Downloads the object at <paramref name="key"/> as a readable stream.
    /// Caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    /// <summary>Permanently deletes the object at <paramref name="key"/>.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Copies the object at <paramref name="sourceKey"/> to <paramref name="destKey"/>
    /// then deletes the source — S3 does not have a native move operation.
    /// </summary>
    Task MoveAsync(string sourceKey, string destKey, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Generates a short-lived pre-signed URL for the object at <paramref name="key"/>.
    /// The URL is valid for <paramref name="expiry"/> (default 15 minutes) and grants
    /// temporary GET access without requiring credentials from the caller (AC-1, Edge Case 2).
    /// </summary>
    Task<string> GeneratePreSignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}

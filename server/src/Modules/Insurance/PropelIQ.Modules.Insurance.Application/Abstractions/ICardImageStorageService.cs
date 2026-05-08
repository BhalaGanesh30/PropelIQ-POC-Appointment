namespace PropelIQ.Modules.Insurance.Application.Abstractions;

/// <summary>
/// Abstraction for Cloudflare R2 (S3-compatible) insurance card image storage
/// (EP-005 US_038 AC-1, AC-3, NFR-007).
///
/// Objects are stored under the key pattern
/// <c>insurance/{patientId}/{profileId}/{side}.{ext}</c> to prevent enumeration.
/// All objects are uploaded with server-side AES-256 encryption (SSE-S3).
/// Pre-signed retrieval URLs expire after 5 minutes to minimise window of exposure.
/// </summary>
public interface ICardImageStorageService
{
    /// <summary>
    /// Uploads a card image to R2 with SSE-S3 server-side encryption enabled (AC-1).
    /// </summary>
    /// <param name="patientId">UUID of the patient — used in the R2 object key path.</param>
    /// <param name="profileId">UUID of the insurance profile — scopes the key to the profile.</param>
    /// <param name="side"><c>front</c> or <c>back</c>.</param>
    /// <param name="fileStream">Seekable stream of the validated file content.</param>
    /// <param name="contentType">MIME type (e.g. <c>image/jpeg</c>).</param>
    /// <param name="extension">Detected file extension from magic-byte validation (<c>jpg</c>, <c>png</c>, <c>pdf</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The R2 object key under which the file was stored.</returns>
    Task<string> UploadAsync(
        Guid patientId,
        Guid profileId,
        string side,
        Stream fileStream,
        string contentType,
        string extension,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a time-limited pre-signed GET URL for the specified R2 object (AC-1).
    /// The URL expires after 5 minutes.
    /// </summary>
    /// <param name="objectKey">R2 object key as returned by <see cref="UploadAsync"/>.</param>
    /// <param name="ct">Cancellation token (reserved for future async signing implementations).</param>
    /// <returns>Pre-signed URL and its UTC expiry time.</returns>
    Task<(string Url, DateTimeOffset ExpiresAt)> GetPreSignedUrlAsync(
        string objectKey,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an object from R2 — used when re-uploading replaces an existing image.
    /// </summary>
    /// <param name="objectKey">R2 object key to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string objectKey, CancellationToken ct = default);
}

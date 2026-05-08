using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Infrastructure.Configuration;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// Cloudflare R2 implementation of <see cref="ICardImageStorageService"/> using the
/// S3-compatible API (EP-005 US_038 AC-1, AC-3, NFR-007).
///
/// Key design decisions:
/// - Objects stored under <c>insurance/{patientId}/{profileId}/{side}.{ext}</c>
///   to prevent direct enumeration of patient records (OWASP A01).
/// - <c>ServerSideEncryptionMethod.AES256</c> (SSE-S3) applied to all uploads
///   satisfying NFR-007 "encryption at rest".
/// - Pre-signed URLs expire after 5 minutes to limit exposure window (AC-1).
/// - <see cref="AmazonS3Client"/> is created once per application lifetime
///   (configured as a singleton in DI) — the client is thread-safe.
/// - R2 credentials come from Vault-managed environment variables
///   (<c>CLOUDFLARE_R2_ACCESS_KEY_ID</c> / <c>CLOUDFLARE_R2_SECRET_ACCESS_KEY</c>)
///   with appsettings values as a non-production fallback.
/// - Credentials are NEVER logged.
/// </summary>
public sealed class R2CardImageStorageService : ICardImageStorageService, IDisposable
{
    private static readonly TimeSpan PreSignedUrlExpiry = TimeSpan.FromMinutes(5);

    private readonly AmazonS3Client _s3;
    private readonly string _bucketName;
    private readonly ILogger<R2CardImageStorageService> _logger;

    public R2CardImageStorageService(
        R2Configuration config,
        ILogger<R2CardImageStorageService> logger)
    {
        _bucketName = config.BucketName;
        _logger = logger;

        // Prefer Vault-injected environment variables; fall back to config values for dev.
        var accessKey = Environment.GetEnvironmentVariable("CLOUDFLARE_R2_ACCESS_KEY_ID")
                        ?? config.AccessKeyId;
        var secretKey = Environment.GetEnvironmentVariable("CLOUDFLARE_R2_SECRET_ACCESS_KEY")
                        ?? config.SecretAccessKey;

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = config.Endpoint,
            ForcePathStyle = true,                          // Required for R2 compatibility.
            AuthenticationRegion = config.Region,           // "auto" for R2.
            SignatureVersion = "4",
        };

        _s3 = new AmazonS3Client(credentials, s3Config);
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        Guid patientId,
        Guid profileId,
        string side,
        Stream fileStream,
        string contentType,
        string extension,
        CancellationToken ct = default)
    {
        // Enumeration-resistant key: includes both patientId and profileId.
        var objectKey = $"insurance/{patientId:N}/{profileId:N}/{side}.{extension}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = fileStream,
            ContentType = contentType,
            AutoCloseStream = false,
            // SSE-S3 server-side AES-256 encryption (NFR-007).
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };

        await _s3.PutObjectAsync(request, ct);

        _logger.LogInformation(
            "Card image uploaded: bucket={Bucket} key={Key} side={Side} profileId={ProfileId}.",
            _bucketName, objectKey, side, profileId);

        return objectKey;
    }

    /// <inheritdoc />
    public Task<(string Url, DateTimeOffset ExpiresAt)> GetPreSignedUrlAsync(
        string objectKey,
        CancellationToken ct = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(PreSignedUrlExpiry);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Expires = expiresAt.UtcDateTime,
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS,
        };

        // AWS SDK pre-signing is CPU-bound and synchronous; wrap result in Task to
        // satisfy the async interface contract without blocking the thread pool.
        var url = _s3.GetPreSignedURL(request);

        _logger.LogDebug(
            "Pre-signed URL generated for key={Key} expires={ExpiresAt}.",
            objectKey, expiresAt);

        return Task.FromResult((url, expiresAt));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_bucketName, objectKey, ct);

        _logger.LogInformation(
            "Card image deleted: bucket={Bucket} key={Key}.", _bucketName, objectKey);
    }

    public void Dispose() => _s3.Dispose();
}

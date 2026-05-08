using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.ClinicalIntelligence.Application.Abstractions;
using PropelIQ.Modules.Insurance.Infrastructure.Configuration;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 implementation of <see cref="IR2DocumentStorageService"/>.
/// Reuses the <c>R2Configuration</c> and S3-compatible client pattern from US_038.
/// SSE-S3 encryption is applied on all uploads (NFR-007).
/// </summary>
public sealed class R2DocumentStorageService : IR2DocumentStorageService, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucketName;
    private readonly ILogger<R2DocumentStorageService> _logger;

    public R2DocumentStorageService(
        R2Configuration config,
        ILogger<R2DocumentStorageService> logger)
    {
        _bucketName = config.BucketName;
        _logger = logger;

        var accessKey = Environment.GetEnvironmentVariable("CLOUDFLARE_R2_ACCESS_KEY_ID")
                        ?? config.AccessKeyId;
        var secretKey = Environment.GetEnvironmentVariable("CLOUDFLARE_R2_SECRET_ACCESS_KEY")
                        ?? config.SecretAccessKey;

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = config.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = config.Region,
            SignatureVersion = "4",
        };

        _s3 = new AmazonS3Client(credentials, s3Config);
    }

    public async Task UploadAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };

        await _s3.PutObjectAsync(request, ct);
        _logger.LogDebug("Uploaded document to R2: {Key}", key);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var request = new GetObjectRequest { BucketName = _bucketName, Key = key };
        var response = await _s3.GetObjectAsync(request, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest { BucketName = _bucketName, Key = key };
        await _s3.DeleteObjectAsync(request, ct);
        _logger.LogDebug("Deleted R2 object: {Key}", key);
    }

    public async Task MoveAsync(string sourceKey, string destKey, string contentType, CancellationToken ct = default)
    {
        await using var stream = await DownloadAsync(sourceKey, ct);
        await UploadAsync(stream, destKey, contentType, ct);
        await DeleteAsync(sourceKey, ct);
    }

    public Task<string> GeneratePreSignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key        = key,
            Verb       = HttpVerb.GET,
            Expires    = DateTime.UtcNow.Add(expiry),
        };

        // GetPreSignedURL is synchronous in the AWSSDK.S3 client
        var url = _s3.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public void Dispose() => _s3.Dispose();
}

namespace PropelIQ.Modules.Insurance.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Cloudflare R2 object storage (S3-compatible API).
/// Bound from the <c>CloudflareR2</c> configuration section (EP-005 US_038 AC-1, NFR-007).
///
/// In production, all credential properties are populated from Vault-managed secrets
/// injected as environment variables.  The configuration section provides non-secret
/// values (bucket name, endpoint, region) while access key / secret key come from
/// <c>CLOUDFLARE_R2_ACCESS_KEY_ID</c> and <c>CLOUDFLARE_R2_SECRET_ACCESS_KEY</c>
/// environment variables with the appsettings values as development fallback.
///
/// SECURITY: Credential properties MUST NOT be logged.
/// </summary>
public sealed class R2Configuration
{
    public const string SectionName = "CloudflareR2";

    /// <summary>R2 bucket name (e.g. <c>propeliq-insurance</c>).</summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>
    /// R2 S3-compatible endpoint URL.
    /// Format: <c>https://&lt;account-id&gt;.r2.cloudflarestorage.com</c>.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// R2 / AWS region string.  Cloudflare R2 uses <c>auto</c> by convention.
    /// </summary>
    public string Region { get; init; } = "auto";

    /// <summary>
    /// R2 access key ID.  Overridden at runtime by the
    /// <c>CLOUDFLARE_R2_ACCESS_KEY_ID</c> environment variable when set.
    /// </summary>
    public string AccessKeyId { get; init; } = string.Empty;

    /// <summary>
    /// R2 secret access key.  Overridden at runtime by the
    /// <c>CLOUDFLARE_R2_SECRET_ACCESS_KEY</c> environment variable when set.
    /// MUST NOT be logged or included in error messages.
    /// </summary>
    public string SecretAccessKey { get; init; } = string.Empty;
}

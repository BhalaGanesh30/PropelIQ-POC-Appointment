using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropelIQ.Modules.SharedServices.Application.AI;
using PropelIQ.SharedKernel.Caching;

namespace PropelIQ.Modules.SharedServices.Infrastructure.AI;

/// <summary>
/// AES-256-GCM encrypted Redis store for PII redaction token maps (US_054, AC-3).
///
/// Storage format: the token map is JSON-serialised, then AES-256-GCM encrypted with a
/// random 12-byte nonce. The combined bytes (nonce ‖ tag ‖ ciphertext) are Base64-encoded
/// and stored as a plain string value in Redis under key <c>redaction:{correlationId:N}</c>
/// with a 5-minute TTL.
///
/// Security properties:
/// <list type="bullet">
///   <item>Random nonce per write — semantic security (same map stored twice produces different ciphertext).</item>
///   <item>GCM tag — authenticated encryption detects tampering.</item>
///   <item>5-minute TTL — automatic eviction prevents stale PII in cache.</item>
///   <item>Explicit <see cref="DeleteAsync"/> called after successful de-anonymization.</item>
/// </list>
///
/// Key configuration: <c>AI:Redaction:EncryptionKey</c> (Base64-encoded 32-byte AES-256 key).
/// When the key is not configured (empty), a dev-only deterministic fallback is used and a
/// warning is emitted — MUST be configured from secrets vault in production.
/// </summary>
public sealed class RedactionMapStore : IRedactionMapStore
{
    // AES-GCM nonce and tag sizes (RFC 5116 recommended)
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes   = 16;

    private static readonly TimeSpan MapTtl      = TimeSpan.FromMinutes(5);
    private const           string   KeyPrefix   = "redaction:";

    private readonly ICacheService              _cache;
    private readonly PiiRedactionOptions        _options;
    private readonly ILogger<RedactionMapStore> _logger;

    public RedactionMapStore(
        ICacheService cache,
        IOptions<PiiRedactionOptions> options,
        ILogger<RedactionMapStore> logger)
    {
        _cache   = cache;
        _options = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task StoreAsync(
        Guid correlationId,
        Dictionary<string, string> tokenMap,
        CancellationToken ct = default)
    {
        var key       = BuildKey(correlationId);
        var json      = JsonSerializer.Serialize(tokenMap);
        var encrypted = Encrypt(json);

        await _cache.SetAsync(key, encrypted, MapTtl, ct);

        _logger.LogDebug(
            "Stored redaction map for correlation {CorrelationId} with {TokenCount} tokens (TTL 5 min).",
            correlationId, tokenMap.Count);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>?> GetAsync(
        Guid correlationId,
        CancellationToken ct = default)
    {
        var key       = BuildKey(correlationId);
        var encrypted = await _cache.GetAsync<string>(key, ct);

        if (encrypted is null)
        {
            _logger.LogWarning(
                "Redaction map not found for correlation {CorrelationId} — expired or never stored.",
                correlationId);
            return null;
        }

        try
        {
            var json = Decrypt(encrypted);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to decrypt redaction map for correlation {CorrelationId}.",
                correlationId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid correlationId, CancellationToken ct = default)
    {
        var key = BuildKey(correlationId);
        await _cache.RemoveAsync(key, ct);

        _logger.LogDebug(
            "Deleted redaction map for correlation {CorrelationId} after de-anonymization.",
            correlationId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildKey(Guid correlationId)
        => $"{KeyPrefix}{correlationId:N}";

    /// <summary>Derives the AES-256-GCM encryption key from configuration.</summary>
    private byte[] ResolveKey()
    {
        if (!string.IsNullOrEmpty(_options.EncryptionKey))
            return Convert.FromBase64String(_options.EncryptionKey);

        // Dev-only fallback — deterministic from a constant phrase.
        // A warning is emitted here so it is visible in CI and staging logs.
        _logger.LogWarning(
            "AI:Redaction:EncryptionKey is not configured. " +
            "Using insecure dev-only AES key — configure from secrets vault in production.");

        return SHA256.HashData(
            "propeliq-dev-only-encryption-key-CHANGE-IN-PRODUCTION"u8.ToArray());
    }

    private string Encrypt(string plaintext)
    {
        var key           = ResolveKey();
        var nonce         = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBuf  = new byte[plaintextBytes.Length];
        var tagBuf         = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertextBuf, tagBuf);

        // Wire format: nonce(12) ‖ tag(16) ‖ ciphertext
        var combined = new byte[NonceSizeBytes + TagSizeBytes + ciphertextBuf.Length];
        nonce.CopyTo(combined, 0);
        tagBuf.CopyTo(combined, NonceSizeBytes);
        ciphertextBuf.CopyTo(combined, NonceSizeBytes + TagSizeBytes);

        return Convert.ToBase64String(combined);
    }

    private string Decrypt(string encryptedBase64)
    {
        var key      = ResolveKey();
        var combined = Convert.FromBase64String(encryptedBase64);

        var nonce      = combined[..NonceSizeBytes];
        var tag        = combined[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var ciphertext = combined[(NonceSizeBytes + TagSizeBytes)..];
        var plaintext  = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}

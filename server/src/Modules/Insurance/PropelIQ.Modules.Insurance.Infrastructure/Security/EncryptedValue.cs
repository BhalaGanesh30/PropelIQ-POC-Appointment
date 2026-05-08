namespace PropelIQ.Modules.Insurance.Infrastructure.Security;

/// <summary>
/// Value object returned by <see cref="IEncryptionService.Encrypt"/>.
///
/// Carries all metadata needed to decrypt the ciphertext later, including
/// the key version so the correct key can be retrieved during decryption and
/// key rotation (Edge Case 1).
/// </summary>
public sealed record EncryptedValue
{
    /// <summary>Base64-encoded bytes of: <c>IV (16 bytes) || AES-CBC ciphertext</c>.</summary>
    public required string CiphertextBase64 { get; init; }

    /// <summary>Base64-encoded HMAC-SHA256 over the raw <c>IV || ciphertext</c> bytes.</summary>
    public required string HmacBase64 { get; init; }

    /// <summary>Version of the AES-256 key used to encrypt this value.</summary>
    public required int KeyVersion { get; init; }
}

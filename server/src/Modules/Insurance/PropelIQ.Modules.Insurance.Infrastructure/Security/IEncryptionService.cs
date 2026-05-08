namespace PropelIQ.Modules.Insurance.Infrastructure.Security;

/// <summary>
/// Authenticated AES-256 encryption abstraction (EP-005 US_038 AC-1, AC-2, NFR-007).
///
/// Implementations MUST use AES-256-CBC with a random IV and HMAC-SHA256 for
/// tamper detection.  The IV is prepended to the ciphertext so it can be recovered
/// during decryption without separate storage.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using the current active key version.
    /// Generates a fresh random 128-bit IV per call.
    /// </summary>
    /// <param name="plaintext">Plain text to encrypt (must not be null or empty).</param>
    /// <returns>
    /// <see cref="EncryptedValue"/> containing the base64 ciphertext (IV prepended),
    /// HMAC, and key version.
    /// </returns>
    EncryptedValue Encrypt(string plaintext);

    /// <summary>
    /// Decrypts <paramref name="encrypted"/> using the key version stored in the value.
    /// Verifies the HMAC before decrypting to prevent padding oracle attacks.
    /// </summary>
    /// <param name="encrypted">Previously encrypted value.</param>
    /// <returns>Original plaintext string.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when HMAC verification fails (tamper detected).
    /// </exception>
    string Decrypt(EncryptedValue encrypted);

    /// <summary>Returns the currently active key version number.</summary>
    int GetCurrentKeyVersion();
}

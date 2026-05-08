using System.Security.Cryptography;
using System.Text;

namespace PropelIQ.Modules.Insurance.Infrastructure.Security;

/// <summary>
/// AES-256-CBC + HMAC-SHA256 authenticated encryption service (EP-005 US_038 AC-1, NFR-007).
///
/// Algorithm:
///   1. Generate a random 128-bit IV per call (<see cref="RandomNumberGenerator"/>).
///   2. Encrypt with AES-256-CBC (PKCS7 padding) using the current key.
///   3. Compute HMAC-SHA256 over the concatenated bytes <c>IV || ciphertext</c>
///      using the same key (Encrypt-then-MAC pattern).
///   4. Encode both IV+ciphertext and HMAC as Base64 for storage.
///
/// Decryption:
///   1. Look up the key by the stored <see cref="EncryptedValue.KeyVersion"/>.
///   2. Verify HMAC <em>before</em> attempting AES decryption to prevent
///      padding oracle attacks (OWASP A02).
///   3. Throw <see cref="CryptographicException"/> on tamper detection.
///
/// Key MUST be exactly 32 bytes (AES-256).
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly EncryptionKeyProvider _keyProvider;

    public AesEncryptionService(EncryptionKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    /// <inheritdoc />
    public EncryptedValue Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var version = _keyProvider.GetCurrentVersion();
        var key = _keyProvider.GetKey(version);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV(); // Fresh random IV per operation.

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext;

        using (var encryptor = aes.CreateEncryptor())
        using (var ms = new MemoryStream())
        {
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                cs.Write(plaintextBytes, 0, plaintextBytes.Length);

            ciphertext = ms.ToArray();
        }

        // IV is prepended so it can be recovered at decryption time without extra columns.
        byte[] ivAndCiphertext = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, ivAndCiphertext, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, ivAndCiphertext, aes.IV.Length, ciphertext.Length);

        // Encrypt-then-MAC: HMAC covers both IV and ciphertext.
        using var hmac = new HMACSHA256(key);
        byte[] mac = hmac.ComputeHash(ivAndCiphertext);

        // Clear key material from the local array.
        CryptographicOperations.ZeroMemory(key);

        return new EncryptedValue
        {
            CiphertextBase64 = Convert.ToBase64String(ivAndCiphertext),
            HmacBase64 = Convert.ToBase64String(mac),
            KeyVersion = version,
        };
    }

    /// <inheritdoc />
    public string Decrypt(EncryptedValue encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);

        var key = _keyProvider.GetKey(encrypted.KeyVersion);

        byte[] ivAndCiphertext;
        byte[] storedMac;
        try
        {
            ivAndCiphertext = Convert.FromBase64String(encrypted.CiphertextBase64);
            storedMac = Convert.FromBase64String(encrypted.HmacBase64);
        }
        catch (FormatException ex)
        {
            // Do not expose key material in the exception chain.
            CryptographicOperations.ZeroMemory(key);
            throw new CryptographicException("Insurance ciphertext is malformed.", ex);
        }

        // Verify HMAC before decrypting (prevents padding oracle attacks — OWASP A02).
        using (var hmac = new HMACSHA256(key))
        {
            byte[] expectedMac = hmac.ComputeHash(ivAndCiphertext);
            if (!CryptographicOperations.FixedTimeEquals(expectedMac, storedMac))
            {
                CryptographicOperations.ZeroMemory(key);
                throw new CryptographicException(
                    "Insurance record HMAC verification failed — data may have been tampered with.");
            }
        }

        const int IvLength = 16; // AES block size = 128 bits.
        if (ivAndCiphertext.Length < IvLength)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new CryptographicException("Insurance ciphertext is too short to contain an IV.");
        }

        byte[] iv = ivAndCiphertext[..IvLength];
        byte[] ciphertext = ivAndCiphertext[IvLength..];

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        byte[] plaintext;
        try
        {
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(ciphertext);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            plaintext = Encoding.UTF8.GetBytes(reader.ReadToEnd());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <inheritdoc />
    public int GetCurrentKeyVersion() => _keyProvider.GetCurrentVersion();
}

namespace PropelIQ.Modules.Insurance.Infrastructure.Security;

/// <summary>
/// Provides AES-256 encryption keys by version number (EP-005 US_038 Edge Case 1).
///
/// Keys are loaded from Vault-managed secrets via <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
/// (key path: <c>InsuranceEncryption:Keys:{version}</c>).  For local development,
/// fall back to environment variables <c>INSURANCE_ENCRYPTION_KEY_V{version}</c>
/// (base64-encoded 32-byte values).
///
/// Security requirements:
/// - Keys MUST NOT be logged, serialised, or exposed in error messages.
/// - Each version key maps to exactly one 32-byte AES-256 secret.
/// - The current (active) version is set via <c>InsuranceEncryption:CurrentKeyVersion</c>
///   (integer, default 1).
/// </summary>
public sealed class EncryptionKeyProvider
{
    private const string ConfigSectionKey = "InsuranceEncryption:Keys:";
    private const string CurrentVersionConfig = "InsuranceEncryption:CurrentKeyVersion";
    private const string EnvVarPrefix = "INSURANCE_ENCRYPTION_KEY_V";

    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public EncryptionKeyProvider(
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Returns the currently active key version number.</summary>
    public int GetCurrentVersion()
    {
        var raw = _configuration[CurrentVersionConfig];
        return int.TryParse(raw, out var v) ? v : 1;
    }

    /// <summary>
    /// Loads and returns the raw 32-byte AES-256 key for the specified version.
    /// Throws <see cref="InvalidOperationException"/> if the key is missing or
    /// is not exactly 32 bytes after base64-decoding.
    /// </summary>
    /// <param name="version">Key version number.</param>
    /// <returns>32-byte key material.</returns>
    /// <exception cref="InvalidOperationException">Key not configured for this version.</exception>
    public byte[] GetKey(int version)
    {
        // 1. Try IConfiguration (Vault-injected secret or appsettings).
        var configValue = _configuration[$"{ConfigSectionKey}{version}"];

        // 2. Fall back to environment variable (dev / CI).
        if (string.IsNullOrWhiteSpace(configValue))
            configValue = Environment.GetEnvironmentVariable($"{EnvVarPrefix}{version}");

        if (string.IsNullOrWhiteSpace(configValue))
            throw new InvalidOperationException(
                $"Insurance encryption key version {version} is not configured. " +
                $"Set InsuranceEncryption:Keys:{version} or {EnvVarPrefix}{version}.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(configValue);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Insurance encryption key version {version} is not valid base64.", ex);
        }

        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                $"Insurance encryption key version {version} must be exactly 32 bytes " +
                $"(256 bits). Received {keyBytes.Length} bytes.");

        return keyBytes;
    }
}

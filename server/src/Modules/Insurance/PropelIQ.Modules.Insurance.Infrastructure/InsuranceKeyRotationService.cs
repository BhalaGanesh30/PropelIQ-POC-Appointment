using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Insurance.Infrastructure.Security;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// Background service that re-encrypts <c>InsuranceProfile</c> rows whose
/// <c>KeyVersion</c> is less than the current encryption key version
/// (EP-005 US_038 NFR-007 — key rotation, DR-007).
///
/// Only runs when <c>InsuranceEncryption:RotationEnabled</c> is <c>true</c> in
/// configuration (opt-in to prevent unintended background writes).
///
/// Algorithm:
///   1. Every 5 minutes, query batches of up to 100 profiles needing rotation.
///   2. Decrypt each field with the stored key version.
///   3. Re-encrypt with the current key.
///   4. Persist the updated row (KeyVersion, encrypted columns).
///   5. Log progress; halt on persistent errors to avoid data corruption.
///
/// Uses <see cref="IServiceScopeFactory"/> for the scoped <see cref="AppDbContext"/>.
/// The <see cref="IEncryptionService"/> is singleton (key material never changes
/// after startup within a process lifetime).
/// </summary>
public sealed class InsuranceKeyRotationService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEncryptionService _encryption;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InsuranceKeyRotationService> _logger;

    public InsuranceKeyRotationService(
        IServiceScopeFactory scopeFactory,
        IEncryptionService encryption,
        IConfiguration configuration,
        ILogger<InsuranceKeyRotationService> logger)
    {
        _scopeFactory = scopeFactory;
        _encryption = encryption;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InsuranceKeyRotationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Guard — double-check at runtime in case config was hot-reloaded.
            if (!_configuration.GetValue<bool>("InsuranceEncryption:RotationEnabled"))
            {
                await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            await RotateBatchAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("InsuranceKeyRotationService stopped.");
    }

    private async Task RotateBatchAsync(CancellationToken ct)
    {
        var currentVersion = _encryption.GetCurrentKeyVersion();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.InsuranceProfiles
            .Where(p => p.KeyVersion < currentVersion)
            .OrderBy(p => p.KeyVersion)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            _logger.LogDebug("InsuranceKeyRotationService: no profiles need key rotation.");
            return;
        }

        _logger.LogInformation(
            "InsuranceKeyRotationService: rotating {Count} profile(s) to key version {Version}.",
            rows.Count, currentVersion);

        int rotated = 0;
        foreach (var profile in rows)
        {
            try
            {
                // Decrypt with the stored (old) key version.
                string policyNumber;
                string providerName;
                string? groupNumber = null;

                if (profile.KeyVersion > 0
                    && profile.EncryptedPolicyNumber is not null
                    && profile.PolicyNumberHmac is not null
                    && profile.EncryptedProviderName is not null
                    && profile.ProviderNameHmac is not null)
                {
                    policyNumber = _encryption.Decrypt(new EncryptedValue
                    {
                        CiphertextBase64 = profile.EncryptedPolicyNumber,
                        HmacBase64 = profile.PolicyNumberHmac,
                        KeyVersion = profile.KeyVersion,
                    });

                    providerName = _encryption.Decrypt(new EncryptedValue
                    {
                        CiphertextBase64 = profile.EncryptedProviderName,
                        HmacBase64 = profile.ProviderNameHmac,
                        KeyVersion = profile.KeyVersion,
                    });

                    if (profile.EncryptedGroupNumber is not null && profile.GroupNumberHmac is not null)
                    {
                        groupNumber = _encryption.Decrypt(new EncryptedValue
                        {
                            CiphertextBase64 = profile.EncryptedGroupNumber,
                            HmacBase64 = profile.GroupNumberHmac,
                            KeyVersion = profile.KeyVersion,
                        });
                    }
                }
                else
                {
                    // KeyVersion == 0: initial encryption of a plaintext record.
                    policyNumber = profile.MemberId;
                    providerName = profile.PayerName;
                    groupNumber = profile.GroupNumber;
                }

                // Re-encrypt with the current key.
                var encPolicyNumber = _encryption.Encrypt(policyNumber);
                var encProviderName = _encryption.Encrypt(providerName);
                var encGroupNumber = groupNumber is not null
                    ? _encryption.Encrypt(groupNumber)
                    : null;

                profile.EncryptedPolicyNumber = encPolicyNumber.CiphertextBase64;
                profile.PolicyNumberHmac = encPolicyNumber.HmacBase64;
                profile.EncryptedProviderName = encProviderName.CiphertextBase64;
                profile.ProviderNameHmac = encProviderName.HmacBase64;
                profile.EncryptedGroupNumber = encGroupNumber?.CiphertextBase64;
                profile.GroupNumberHmac = encGroupNumber?.HmacBase64;
                profile.KeyVersion = currentVersion;

                rotated++;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                _logger.LogError(ex,
                    "Key rotation failed for InsuranceProfile {ProfileId}. Row skipped.",
                    profile.Id);
            }
        }

        if (rotated > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "InsuranceKeyRotationService: rotated {Rotated}/{Total} profile(s) successfully.",
                rotated, rows.Count);
        }
    }
}

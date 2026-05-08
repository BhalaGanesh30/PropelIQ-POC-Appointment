using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Administration.Domain.Entities;
using PropelIQ.Modules.Insurance.Application.Abstractions;
using PropelIQ.Modules.Insurance.Application.Dto;
using PropelIQ.Modules.Insurance.Infrastructure.Security;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Insurance.Infrastructure;

/// <summary>
/// Persists insurance profiles with AES-256 field-level encryption (EP-005 US_038 AC-1, AC-2).
///
/// Save path   — encrypts PolicyNumber, ProviderName, and GroupNumber before writing.
/// Retrieve path — decrypts transparently using the key version stored per record.
/// Records with KeyVersion == 0 have not yet been encrypted; the plaintext columns
/// (MemberId / PayerName / GroupNumber) are authoritative until the key-rotation
/// service upgrades them (zero-downtime migration).
/// Card images are nullable and stored as-is (Edge Case 2).
/// </summary>
public sealed class InsuranceProfileService : IInsuranceProfileService
{
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<InsuranceProfileService> _logger;

    public InsuranceProfileService(
        AppDbContext db,
        IEncryptionService encryption,
        ILogger<InsuranceProfileService> logger)
    {
        _db = db;
        _encryption = encryption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InsuranceSaveResponse> SaveAsync(
        InsuranceSaveRequest request,
        CancellationToken ct = default)
    {
        var isPrimary = string.Equals(request.Tier, "Primary",
            StringComparison.OrdinalIgnoreCase);

        // Encrypt sensitive PHI fields (AC-1, NFR-007).
        var encPolicyNumber = _encryption.Encrypt(request.PolicyNumber);
        var encProviderName = _encryption.Encrypt(request.ProviderName);
        var encGroupNumber = string.IsNullOrWhiteSpace(request.GroupNumber)
            ? null
            : _encryption.Encrypt(request.GroupNumber);

        var existing = await _db.InsuranceProfiles
            .FirstOrDefaultAsync(
                p => p.PatientId == request.PatientId && p.IsPrimary == isPrimary,
                ct);

        if (existing is not null)
        {
            // Encrypted columns.
            existing.EncryptedPolicyNumber = encPolicyNumber.CiphertextBase64;
            existing.PolicyNumberHmac = encPolicyNumber.HmacBase64;
            existing.EncryptedProviderName = encProviderName.CiphertextBase64;
            existing.ProviderNameHmac = encProviderName.HmacBase64;
            existing.EncryptedGroupNumber = encGroupNumber?.CiphertextBase64;
            existing.GroupNumberHmac = encGroupNumber?.HmacBase64;
            existing.KeyVersion = encPolicyNumber.KeyVersion;

            // Keep legacy plaintext columns for zero-downtime reads while rotation runs.
            existing.MemberId = request.PolicyNumber;
            existing.PayerName = request.ProviderName;
            existing.GroupNumber = request.GroupNumber;
            existing.ProviderCode = request.ProviderCode;
            existing.VerificationStatus = request.ValidationStatus;
            existing.CardImageFrontPath = request.CardImageFrontPath;  // nullable (Edge Case 2)
            existing.CardImageBackPath = request.CardImageBackPath;    // nullable (Edge Case 2)

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Updated InsuranceProfile {ProfileId} (encrypted) for PatientId={PatientId} tier={Tier}.",
                existing.Id, request.PatientId, request.Tier);

            return BuildSaveResponse(existing.Id, request);
        }

        // Insert new encrypted profile.
        var profile = new InsuranceProfile
        {
            PatientId = request.PatientId,
            MemberId = request.PolicyNumber,
            PayerName = request.ProviderName,
            IsPrimary = isPrimary,
            ProviderCode = request.ProviderCode,
            GroupNumber = request.GroupNumber,
            VerificationStatus = request.ValidationStatus,
            CardImageFrontPath = request.CardImageFrontPath,
            CardImageBackPath = request.CardImageBackPath,

            EncryptedPolicyNumber = encPolicyNumber.CiphertextBase64,
            PolicyNumberHmac = encPolicyNumber.HmacBase64,
            EncryptedProviderName = encProviderName.CiphertextBase64,
            ProviderNameHmac = encProviderName.HmacBase64,
            EncryptedGroupNumber = encGroupNumber?.CiphertextBase64,
            GroupNumberHmac = encGroupNumber?.HmacBase64,
            KeyVersion = encPolicyNumber.KeyVersion,
        };

        _db.InsuranceProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created InsuranceProfile {ProfileId} (encrypted) for PatientId={PatientId} tier={Tier}.",
            profile.Id, request.PatientId, request.Tier);

        return BuildSaveResponse(profile.Id, request);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InsuranceProfileDto>> GetByPatientIdAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        var profiles = await _db.InsuranceProfiles
            .AsNoTracking()
            .Where(p => p.PatientId == patientId)
            .ToListAsync(ct);

        var result = new List<InsuranceProfileDto>(profiles.Count);
        foreach (var p in profiles)
        {
            var dto = DecryptProfile(p);
            if (dto is not null)
                result.Add(dto);
        }

        return result.AsReadOnly();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static InsuranceSaveResponse BuildSaveResponse(Guid profileId, InsuranceSaveRequest request) =>
        new()
        {
            ProfileId = profileId,
            PatientId = request.PatientId,
            Tier = request.Tier,
            ValidationStatus = request.ValidationStatus,
        };

    /// <summary>
    /// Decrypts a profile row.
    /// Falls back to plaintext columns for rows with KeyVersion == 0 (not yet migrated).
    /// Returns null and logs an error when HMAC verification fails (tamper detected).
    /// </summary>
    private InsuranceProfileDto? DecryptProfile(InsuranceProfile profile)
    {
        string policyNumber;
        string providerName;
        string? groupNumber;

        if (profile.KeyVersion > 0
            && profile.EncryptedPolicyNumber is not null
            && profile.PolicyNumberHmac is not null
            && profile.EncryptedProviderName is not null
            && profile.ProviderNameHmac is not null)
        {
            try
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

                groupNumber = (profile.EncryptedGroupNumber is not null
                    && profile.GroupNumberHmac is not null)
                    ? _encryption.Decrypt(new EncryptedValue
                    {
                        CiphertextBase64 = profile.EncryptedGroupNumber,
                        HmacBase64 = profile.GroupNumberHmac,
                        KeyVersion = profile.KeyVersion,
                    })
                    : null; // Edge Case 2: no group number on record.
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                _logger.LogError(ex,
                    "HMAC verification failed for InsuranceProfile {ProfileId}. Record excluded.",
                    profile.Id);
                return null;
            }
        }
        else
        {
            // Fallback to plaintext columns (pre-rotation records).
            policyNumber = profile.MemberId;
            providerName = profile.PayerName;
            groupNumber = profile.GroupNumber;
        }

        return new InsuranceProfileDto
        {
            ProfileId = profile.Id,
            PatientId = profile.PatientId,
            Tier = profile.IsPrimary ? "Primary" : "Secondary",
            PolicyNumber = policyNumber,
            ProviderCode = profile.ProviderCode ?? string.Empty,
            ProviderName = providerName,
            GroupNumber = groupNumber,
            ValidationStatus = profile.VerificationStatus,
            // Return dedicated R2 key columns (task_003); fall back to legacy path columns
            // for records that pre-date the R2 migration.
            CardImageFrontPath = profile.CardImageFrontKey ?? profile.CardImageFrontPath,
            CardImageBackPath = profile.CardImageBackKey ?? profile.CardImageBackPath,
        };
    }

    /// <inheritdoc />
    public async Task UpdateCardImageKeyAsync(
        Guid profileId,
        string side,
        string? objectKey,
        CancellationToken ct = default)
    {
        var profile = await _db.InsuranceProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId, ct);

        if (profile is null)
        {
            _logger.LogWarning(
                "UpdateCardImageKeyAsync: InsuranceProfile {ProfileId} not found.", profileId);
            return;
        }

        var isFront = string.Equals(side, "front", StringComparison.OrdinalIgnoreCase);

        // Store R2 object key in the dedicated column (task_003), not the legacy path column.
        if (isFront)
            profile.CardImageFrontKey = objectKey;
        else
            profile.CardImageBackKey = objectKey;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Card image key updated for InsuranceProfile {ProfileId} side={Side}.",
            profileId, side);
    }
}

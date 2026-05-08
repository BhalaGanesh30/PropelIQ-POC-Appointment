using PropelIQ.SharedKernel;

namespace PropelIQ.Modules.Administration.Domain.Entities;

public sealed class InsuranceProfile : BaseEntity
{
    public required Guid PatientId { get; set; }

    // Legacy field names kept for backward compatibility with pre-EP-005 consumers.
    // PolicyNumber is stored as MemberId; ProviderName is stored as PayerName.
    public required string PayerName { get; set; }
    public required string MemberId { get; set; }
    public bool IsPrimary { get; set; }
    public string VerificationStatus { get; set; } = "SoftValidated";

    // EP-005 US_037: extended fields for soft validation engine (task_002).
    public string? ProviderCode { get; set; }
    public string? GroupNumber { get; set; }

    // Legacy local file-path columns (US_037).  Retained for zero-downtime
    // backward compatibility; R2 object keys are stored in the dedicated columns below.
    public string? CardImageFrontPath { get; set; }
    public string? CardImageBackPath { get; set; }

    // EP-005 US_038: Cloudflare R2 object keys for card images (task_003).
    // Nullable — card images are optional (Edge Case 2).
    public string? CardImageFrontKey { get; set; }
    public string? CardImageBackKey { get; set; }

    // EP-005 US_038: AES-256 encrypted field storage (task_001).
    // Plaintext fields (MemberId/PayerName/GroupNumber) are retained for
    // zero-downtime migration reads; once all rows are re-encrypted they can be
    // cleared.  Encrypted columns hold Base64(IV || ciphertext).
    public string? EncryptedPolicyNumber { get; set; }
    public string? PolicyNumberHmac { get; set; }
    public string? EncryptedProviderName { get; set; }
    public string? ProviderNameHmac { get; set; }
    public string? EncryptedGroupNumber { get; set; }
    public string? GroupNumberHmac { get; set; }

    /// <summary>
    /// Version of the AES-256 key used to encrypt this record's fields.
    /// 0 = not yet encrypted (plaintext columns still authoritative).
    /// </summary>
    public int KeyVersion { get; set; } = 0;

    public Patient Patient { get; set; } = null!;
}

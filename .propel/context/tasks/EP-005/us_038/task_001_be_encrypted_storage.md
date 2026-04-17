---
task_id: task_001
user_story: us_038
epic: EP-005
layer: Backend
status: not-started
effort_hours: 8
---

# Task - task_001

## Requirement Reference

- **User Story**: [us_038] Secure Insurance Data Storage
- **Story Location**: [.propel/context/tasks/EP-005/us_038/us_038.md](.propel/context/tasks/EP-005/us_038/us_038.md)
- **Acceptance Criteria**:
  - AC-1: Given I submit my insurance details including card image, When the data is persisted, Then all insurance fields are encrypted using AES-256 before storage and card images are saved in encrypted cloud storage (Cloudflare R2).
  - AC-2: Given encrypted insurance data is retrieved by the API, When an authorized user accesses the insurance profile, Then the data is decrypted transparently in the application layer before being returned in the response.
  - AC-4: Given an unauthorized user attempts to access insurance data, When the request is processed, Then the API returns HTTP 403 and no insurance data is exposed.
- **Edge Cases**:
  - Edge Case 1: Encryption key is rotated — key rotation process re-encrypts all insurance records; a rollback key is retained for a transition period.
  - Edge Case 2: Insurance records with missing card images — card image is optional; records without images are stored with a null image reference; no validation error is raised.

---

## Design References (Backend Task)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular | 17.x |
| Backend | ASP.NET Core Web API | 8.x |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15.x |
| Cache | Redis (StackExchange.Redis) | 2.x |
| Auth | ASP.NET Core Identity + JWT | 8.x |
| Encryption | .NET System.Security.Cryptography (AES-256) | 8.x |
| Secret Management | Vault-managed secrets | latest stable |
| Observability | OpenTelemetry .NET | 1.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the field-level AES-256 encryption and transparent decryption service for insurance profile data in the `Insurance` module of the ASP.NET Core 8 Web API. This task creates an `IEncryptionService` abstraction that encrypts sensitive insurance fields (policy number, provider name, group number) before persistence and decrypts them transparently on retrieval (AC-2). The encryption uses AES-256-CBC with HMAC-SHA256 for authenticated encryption via `System.Security.Cryptography`. The encryption key is sourced from Vault-managed secrets (environment variable fallback for development) and never hardcoded. The service supports key rotation by maintaining a `key_version` field on each encrypted record: on read, the correct key is selected; a background key rotation job re-encrypts records using the new key while retaining the rollback key during the transition period (Edge Case 1). The `InsuranceProfileService` (from US_037/task_002) is extended with encryption/decryption calls in the save and retrieve paths. The `GET /api/v1/insurance/{patientId}` endpoint is secured with `[Authorize]` and an ownership/role check — patients can access only their own records, staff can access any — returning HTTP 403 for unauthorized access (AC-4). Card images with null references are handled gracefully (Edge Case 2).

---

## Dependent Tasks

- **us_037/task_002** — `InsuranceProfileService` and `InsuranceController` must exist; this task extends them with encryption.
- **us_037/task_003** — `insurance_profiles` table with `validation_status` must exist.
- **us_038/task_003** — `key_version` column and encrypted field columns must be added to `insurance_profiles`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `IEncryptionService` | CREATE | Interface: `Encrypt(plaintext, keyVersion)`, `Decrypt(ciphertext, keyVersion)`, `GetCurrentKeyVersion()` |
| `AesEncryptionService` | CREATE | AES-256-CBC with HMAC-SHA256, key loaded from Vault/env, key version support |
| `EncryptionKeyProvider` | CREATE | Loads encryption keys by version from Vault-managed secrets or environment variables |
| `InsuranceProfileService` | MODIFY | Inject `IEncryptionService`; encrypt fields on save, decrypt on retrieve |
| `InsuranceController` | MODIFY | Add `GET /api/v1/insurance/{patientId}` with ownership/role authorization check |
| `InsuranceProfileDto` | CREATE | Response DTO with decrypted fields returned to authorized callers |
| `InsuranceKeyRotationService` | CREATE | `BackgroundService` for re-encrypting records during key rotation |
| `InsuranceModule` DI registration | MODIFY | Register encryption services |

---

## Implementation Plan

1. **Create `EncryptionKeyProvider`** in `Insurance/Security/EncryptionKeyProvider.cs`: Load AES-256 keys by version from Vault-managed secrets (via `IConfiguration` with Vault provider). Fallback to environment variables (`INSURANCE_ENCRYPTION_KEY_V1`, `INSURANCE_ENCRYPTION_KEY_V2`) for local development. Expose `GetKey(int version)` returning `byte[32]` and `GetCurrentVersion()` returning the active key version. Keys MUST NOT be logged or serialised.
2. **Create `IEncryptionService` and `AesEncryptionService`**: Implement `Encrypt(string plaintext): EncryptedValue` and `Decrypt(EncryptedValue ciphertext): string`. Use `Aes.Create()` with 256-bit key, CBC mode, PKCS7 padding. Generate a random IV per encryption operation. Prepend IV to ciphertext. Compute HMAC-SHA256 over `(IV + ciphertext)` for tamper detection. Return `EncryptedValue` record containing `CiphertextBase64`, `KeyVersion`, `HmacBase64`. On decryption, verify HMAC before decrypting to prevent padding oracle attacks. Throw `CryptographicException` on tamper detection.
3. **Modify `InsuranceProfileService.SaveAsync`**: Before persisting to EF Core, encrypt `PolicyNumber`, `ProviderName`, and `GroupNumber` fields via `IEncryptionService.Encrypt()`. Store encrypted values in the `encrypted_*` columns (from task_003). Store the `key_version`. Card image path is stored as-is (nullable per Edge Case 2).
4. **Modify `InsuranceProfileService` — add `GetByPatientIdAsync`**: Query the `insurance_profiles` table by `patient_id`. Decrypt `PolicyNumber`, `ProviderName`, and `GroupNumber` via `IEncryptionService.Decrypt()` using the stored `key_version`. Map to `InsuranceProfileDto`. Return null if not found.
5. **Add `GET /api/v1/insurance/{patientId}` to `InsuranceController`**: Apply `[Authorize]`. In the action method, extract the caller's `userId` and `role` from JWT claims. If role is `Patient`, verify `patientId` matches the caller's patient ID — return `403 Forbidden` if mismatch (AC-4). If role is `Staff` or `Admin`, allow access. Call `InsuranceProfileService.GetByPatientIdAsync()` and return the decrypted DTO.
6. **Implement `InsuranceKeyRotationService`** as a `BackgroundService`: Triggered by configuration flag (`InsuranceEncryption:RotationEnabled = true`). Query all `insurance_profiles` where `key_version < currentVersion`. For each record: decrypt with old key, re-encrypt with new key, update `key_version`. Process in batches of 100 with `SaveChangesAsync` per batch to avoid long transactions. Log progress via `ILogger`. Use `IServiceScopeFactory` for scoped DbContext.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Insurance/
│   │   ├── Controllers/
│   │   │   └── InsuranceController.cs                 ← MODIFY (add GET endpoint with auth check)
│   │   ├── Services/
│   │   │   ├── IInsuranceValidationService.cs         ← EXISTS (us_037)
│   │   │   ├── InsuranceValidationService.cs          ← EXISTS (us_037)
│   │   │   ├── IInsuranceProfileService.cs            ← MODIFY (add GetByPatientIdAsync)
│   │   │   ├── InsuranceProfileService.cs             ← MODIFY (add encryption/decryption)
│   │   │   └── InsuranceKeyRotationService.cs         ← CREATE
│   │   ├── Security/
│   │   │   ├── IEncryptionService.cs                  ← CREATE
│   │   │   ├── AesEncryptionService.cs                ← CREATE
│   │   │   └── EncryptionKeyProvider.cs               ← CREATE
│   │   └── DTOs/
│   │       ├── InsuranceProfileDto.cs                 ← CREATE
│   │       └── [existing DTOs from us_037...]
│   └── [existing modules...]
├── Program.cs                                          ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Insurance/Security/IEncryptionService.cs` | Interface for AES-256 encrypt/decrypt with key versioning |
| CREATE | `Server/Modules/Insurance/Security/AesEncryptionService.cs` | AES-256-CBC + HMAC-SHA256 implementation with random IV per operation |
| CREATE | `Server/Modules/Insurance/Security/EncryptionKeyProvider.cs` | Key loader from Vault/env variables with version support |
| CREATE | `Server/Modules/Insurance/Services/InsuranceKeyRotationService.cs` | `BackgroundService` for batch re-encryption during key rotation |
| CREATE | `Server/Modules/Insurance/DTOs/InsuranceProfileDto.cs` | Response DTO with decrypted insurance fields |
| MODIFY | `Server/Modules/Insurance/Services/IInsuranceProfileService.cs` | Add `GetByPatientIdAsync` method signature |
| MODIFY | `Server/Modules/Insurance/Services/InsuranceProfileService.cs` | Encrypt on save, decrypt on retrieve using `IEncryptionService` |
| MODIFY | `Server/Modules/Insurance/Controllers/InsuranceController.cs` | Add `GET /api/v1/insurance/{patientId}` with ownership/role auth check |
| MODIFY | `Server/Program.cs` | Register `IEncryptionService`, `EncryptionKeyProvider`, `InsuranceKeyRotationService` |

---

## External References

- .NET AES encryption: https://learn.microsoft.com/en-us/dotnet/standard/security/encrypting-data
- .NET HMAC-SHA256: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256
- ASP.NET Core 8 data protection and key management: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-8.0
- ASP.NET Core 8 resource-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-8.0
- OWASP cryptographic failures: https://owasp.org/Top10/A02_2021-Cryptographic_Failures/
- FR-IP-002: System MUST store primary and secondary insurance details and images using encryption at rest
- NFR-007: System MUST encrypt protected health information at rest using AES-256 and enforce TLS 1.3
- DR-008: System MUST isolate tenant-level operational data and restrict direct access to PHI columns
- TR-006: Centralized identity and authorization with role claims and endpoint policy enforcement

---

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run API locally
dotnet run --project Server/Server.csproj

# Run tests
dotnet test
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `AesEncryptionService` (encrypt/decrypt round-trip, tamper detection, key versioning)
- [ ] Unit tests pass for `InsuranceProfileService` (encrypted save, decrypted retrieve)
- [ ] Integration tests pass for `GET /api/v1/insurance/{patientId}` — patient accessing own data returns 200 with decrypted fields
- [ ] Integration tests pass for `GET /api/v1/insurance/{patientId}` — patient accessing another patient's data returns 403
- [ ] Integration tests pass for `GET /api/v1/insurance/{patientId}` — unauthenticated returns 401
- [ ] Integration tests pass for `GET /api/v1/insurance/{patientId}` — staff accessing any patient returns 200
- [ ] Encryption key never appears in logs (verified via log output inspection)
- [ ] Key rotation service re-encrypts records in batches correctly

---

## Implementation Checklist

- [ ] Create `EncryptionKeyProvider` loading AES-256 keys from Vault/environment variables with version support
- [ ] Implement `AesEncryptionService` with AES-256-CBC, random IV, and HMAC-SHA256 tamper detection
- [ ] Modify `InsuranceProfileService.SaveAsync` to encrypt policy number, provider name, and group number before persistence
- [ ] Implement `InsuranceProfileService.GetByPatientIdAsync` with transparent decryption using stored key version
- [ ] Add `GET /api/v1/insurance/{patientId}` with ownership check (patient own-record only) and role-based access (staff/admin any record)
- [ ] Implement `InsuranceKeyRotationService` as `BackgroundService` for batch re-encryption during key rotation (Edge Case 1)
- [ ] Handle null card image references gracefully in save and retrieve paths (Edge Case 2)
- [ ] Register all encryption services and key rotation background service in DI

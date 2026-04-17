---
task_id: task_002
user_story: us_038
epic: EP-005
layer: Backend
status: not-started
effort_hours: 6
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_038] Secure Insurance Data Storage
- **Story Location**: [.propel/context/tasks/EP-005/us_038/us_038.md](.propel/context/tasks/EP-005/us_038/us_038.md)
- **Acceptance Criteria**:
  - AC-1: Given I submit my insurance details including card image, When the data is persisted, Then card images are saved in encrypted cloud storage (Cloudflare R2).
  - AC-3: Given insurance card images are uploaded, When the upload is processed, Then the file is validated for type (JPG, PNG, PDF) and size (max 10 MB) before encrypted persistence.
  - AC-4: Given an unauthorized user attempts to access insurance data, When the request is processed, Then the API returns HTTP 403 and no insurance data is exposed.
- **Edge Cases**:
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
| Cloud Storage | Cloudflare R2 (S3-compatible API) | latest |
| Auth | ASP.NET Core Identity + JWT | 8.x |
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

Implement the insurance card image upload pipeline with Cloudflare R2 encrypted storage. This task creates an `ICardImageStorageService` that handles file validation (type and size per AC-3), uploads card images to Cloudflare R2 using the S3-compatible API with server-side encryption enabled, and generates time-limited pre-signed URLs for authorised retrieval. The upload endpoint `POST /api/v1/insurance/{patientId}/card-image` validates that the file is JPG, PNG, or PDF and does not exceed 10 MB before streaming to R2. The file is stored under a path keyed by `patient_id` and `insurance_id` to prevent enumeration attacks. A retrieval endpoint `GET /api/v1/insurance/{patientId}/card-image/{side}` generates a pre-signed R2 URL (5-minute expiry) for authorised callers, with the same ownership/role check as the insurance profile endpoint (AC-4). Card images are optional — upload endpoints accept null/empty file gracefully (Edge Case 2). The R2 bucket is configured with server-side encryption (SSE-S3) satisfying NFR-007. R2 credentials are loaded from Vault-managed secrets.

---

## Dependent Tasks

- **us_037/task_002** — `InsuranceController` must exist; this task adds card image upload/retrieval endpoints to it.
- **us_038/task_001** — `IEncryptionService` and ownership authorization pattern must exist; reused for auth checks.
- **us_038/task_003** — `card_image_front_key` and `card_image_back_key` columns must exist on `insurance_profiles`.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `ICardImageStorageService` | CREATE | Interface: `UploadAsync`, `GetPreSignedUrlAsync`, `DeleteAsync` |
| `R2CardImageStorageService` | CREATE | Cloudflare R2 S3-compatible implementation using `AWSSDK.S3` client |
| `CardImageValidator` | CREATE | Static validation: file type (JPG/PNG/PDF by magic bytes), size (max 10 MB) |
| `InsuranceController` | MODIFY | Add `POST /api/v1/insurance/{patientId}/card-image` and `GET /api/v1/insurance/{patientId}/card-image/{side}` |
| `InsuranceProfileService` | MODIFY | Update `card_image_front_key` and `card_image_back_key` after upload |
| `R2Configuration` | CREATE | Options class for R2 bucket name, endpoint, access key, secret key |
| `InsuranceModule` DI registration | MODIFY | Register `ICardImageStorageService` and `R2Configuration` |

---

## Implementation Plan

1. **Create `R2Configuration`** options class in `Insurance/Configuration/R2Configuration.cs`: Properties for `BucketName`, `Endpoint`, `AccessKeyId`, `SecretAccessKey`, `Region` (default `auto`). Bind from `IConfiguration` section `CloudflareR2`. Load credentials from Vault-managed secrets (environment variable fallback for development). Credentials MUST NOT be logged.
2. **Create `CardImageValidator`** in `Insurance/Validation/CardImageValidator.cs`: Static method `Validate(IFormFile file): ValidationResult`. Check file size does not exceed 10 MB (`10 * 1024 * 1024` bytes). Validate file type by reading magic bytes (first 4-8 bytes): JPEG (`0xFF 0xD8 0xFF`), PNG (`0x89 0x50 0x4E 0x47`), PDF (`0x25 0x50 0x44 0x46`). Do NOT rely solely on file extension or content-type header to prevent content-type spoofing. Return descriptive validation errors on failure.
3. **Create `ICardImageStorageService` and `R2CardImageStorageService`**:
   - Use `AWSSDK.S3` NuGet package with `AmazonS3Client` configured for Cloudflare R2 endpoint.
   - `UploadAsync(Guid patientId, Guid insuranceId, string side, Stream fileStream, string contentType)`: Upload to R2 with key pattern `insurance/{patientId}/{insuranceId}/{side}.{ext}`. Enable `ServerSideEncryptionMethod.AES256` (SSE-S3). Return the object key.
   - `GetPreSignedUrlAsync(string objectKey)`: Generate pre-signed GET URL with 5-minute expiry using `GetPreSignedUrlRequest`.
   - `DeleteAsync(string objectKey)`: Delete object from R2 for cleanup/re-upload scenarios.
4. **Add upload endpoint to `InsuranceController`**: `POST /api/v1/insurance/{patientId}/card-image` accepts `[FromForm] IFormFile file` and `[FromQuery] string side` (values: `front`, `back`). Apply `[Authorize]` with ownership/role check (same pattern as task_001). Validate file via `CardImageValidator`. If file is null or empty, return `200 OK` with no-op (Edge Case 2). On valid file, call `cardImageStorageService.UploadAsync()`. Update `InsuranceProfileService` to store the R2 object key in `card_image_front_key` or `card_image_back_key`. Return `201 Created` with object key.
5. **Add retrieval endpoint to `InsuranceController`**: `GET /api/v1/insurance/{patientId}/card-image/{side}` with `[Authorize]` and ownership/role check. Query the insurance profile for the object key. If key is null, return `404 Not Found`. Generate pre-signed URL via `cardImageStorageService.GetPreSignedUrlAsync()`. Return the URL in the response (client redirects or fetches directly).
6. **Register DI**: Bind `R2Configuration` from configuration. Register `ICardImageStorageService` → `R2CardImageStorageService` as scoped. Add `AWSSDK.S3` NuGet package reference.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Insurance/
│   │   ├── Controllers/
│   │   │   └── InsuranceController.cs                 ← MODIFY (add card image endpoints)
│   │   ├── Services/
│   │   │   ├── InsuranceProfileService.cs             ← MODIFY (update card image keys)
│   │   │   ├── ICardImageStorageService.cs            ← CREATE
│   │   │   ├── R2CardImageStorageService.cs           ← CREATE
│   │   │   └── [existing services...]
│   │   ├── Configuration/
│   │   │   └── R2Configuration.cs                     ← CREATE
│   │   ├── Validation/
│   │   │   └── CardImageValidator.cs                  ← CREATE
│   │   └── DTOs/
│   │       └── [existing DTOs...]
│   └── [existing modules...]
├── Server.csproj                                       ← MODIFY (add AWSSDK.S3 package)
├── Program.cs                                          ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Insurance/Services/ICardImageStorageService.cs` | Interface for R2 upload, pre-signed URL, and delete operations |
| CREATE | `Server/Modules/Insurance/Services/R2CardImageStorageService.cs` | Cloudflare R2 S3-compatible implementation with SSE-S3 encryption |
| CREATE | `Server/Modules/Insurance/Configuration/R2Configuration.cs` | Options class for R2 credentials and bucket configuration |
| CREATE | `Server/Modules/Insurance/Validation/CardImageValidator.cs` | Magic-byte file type validation (JPG/PNG/PDF) and 10 MB size check |
| MODIFY | `Server/Modules/Insurance/Controllers/InsuranceController.cs` | Add `POST card-image` upload and `GET card-image/{side}` retrieval endpoints |
| MODIFY | `Server/Modules/Insurance/Services/InsuranceProfileService.cs` | Update card image key references after R2 upload |
| MODIFY | `Server/Server.csproj` | Add `AWSSDK.S3` NuGet package reference |
| MODIFY | `Server/Program.cs` | Register `R2Configuration`, `ICardImageStorageService` |

---

## External References

- AWSSDK.S3 NuGet package (S3-compatible for R2): https://www.nuget.org/packages/AWSSDK.S3
- Cloudflare R2 S3 API compatibility: https://developers.cloudflare.com/r2/api/s3/api/
- Cloudflare R2 pre-signed URLs: https://developers.cloudflare.com/r2/api/s3/presigned-urls/
- ASP.NET Core 8 file uploads: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-8.0
- OWASP file upload security: https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html
- Magic byte signatures for file validation: https://en.wikipedia.org/wiki/List_of_file_signatures
- FR-IP-002: System MUST store primary and secondary insurance details and images using encryption at rest
- NFR-007: System MUST encrypt protected health information at rest using AES-256 and enforce TLS 1.3
- DR-004: System MUST retain patient records and uploaded documents for 10 years

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

- [ ] Unit tests pass for `CardImageValidator` (valid JPG/PNG/PDF accepted, invalid types rejected, oversized files rejected)
- [ ] Unit tests pass for `R2CardImageStorageService` (mock S3 client, verify upload/download/delete calls)
- [ ] Integration tests pass for `POST /api/v1/insurance/{patientId}/card-image` — valid file returns 201
- [ ] Integration tests pass for `POST /api/v1/insurance/{patientId}/card-image` — invalid file type returns 400
- [ ] Integration tests pass for `POST /api/v1/insurance/{patientId}/card-image` — oversized file returns 400
- [ ] Integration tests pass for `POST /api/v1/insurance/{patientId}/card-image` — null file returns 200 (no-op)
- [ ] Integration tests pass for `GET /api/v1/insurance/{patientId}/card-image/front` — returns pre-signed URL
- [ ] Authorization verified: patient accessing another patient's card image returns 403

---

## Implementation Checklist

- [ ] Create `R2Configuration` options class bound from `CloudflareR2` config section with Vault-managed credentials
- [ ] Implement `CardImageValidator` with magic-byte file type validation (JPG/PNG/PDF) and 10 MB size limit
- [ ] Implement `R2CardImageStorageService` using `AWSSDK.S3` with SSE-S3 server-side encryption and pre-signed URL generation (5-min expiry)
- [ ] Add `POST /api/v1/insurance/{patientId}/card-image` endpoint with ownership/role auth check and file validation
- [ ] Add `GET /api/v1/insurance/{patientId}/card-image/{side}` endpoint with pre-signed URL response
- [ ] Handle null/empty file upload gracefully — no validation error raised (Edge Case 2)

---
task_id: task_002
user_story: us_037
epic: EP-005
layer: Backend
status: not-started
effort_hours: 7
---

# Task - task_002

## Requirement Reference

- **User Story**: [us_037] Insurance Soft Validation Engine
- **Story Location**: [.propel/context/tasks/EP-005/us_037/us_037.md](.propel/context/tasks/EP-005/us_037/us_037.md)
- **Acceptance Criteria**:
  - AC-1: Given I enter insurance details during the booking flow, When I submit the insurance form, Then the system validates the policy number format and provider code against the reference database within 500 ms.
  - AC-2: Given the soft validation detects a format mismatch, When the result is returned, Then a warning indicator is displayed but the booking is not blocked.
  - AC-3: Given the insurance details pass soft validation, When the form is submitted, Then the record is saved with a `SoftValidated` status flag.
  - AC-4: Given I submit insurance details that completely fail validation, When the result is returned, Then the system flags the record with `ValidationFailed` status and records the validation result for staff review.
- **Edge Cases**:
  - Edge Case 1: Reference database unavailable — validation is skipped; booking proceeds; insurance record saved with `ValidationPending` status and a background retry is queued.
  - Edge Case 2: Secondary insurance has same policy number as primary — validation flags the duplicate as a potential data entry error with a warning but does not block submission.

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
| Observability | OpenTelemetry .NET | 1.x |
| Background Jobs | .NET BackgroundService | 8.x |
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

Implement the insurance soft validation engine as a service layer in the `Insurance` module of the ASP.NET Core 8 Web API. This task exposes two endpoints: `POST /api/v1/insurance/validate` which performs non-blocking soft validation of insurance details against format rules and a provider reference database within 500 ms (AC-1, NFR-002), and `POST /api/v1/insurance` which persists the insurance profile with the validation status. The validation engine performs two checks: (1) policy number format validation against provider-specific regex patterns from the `insurance_providers` reference table, and (2) provider code existence check against the reference database. Results are categorised as `SoftValidated` (all checks pass), `ValidationFailed` (complete failure — flagged for staff review with recorded details per AC-4), or `ValidationPending` (reference database unreachable — booking continues, background retry queued per Edge Case 1). Duplicate policy number detection between primary and secondary insurance returns a non-blocking warning (Edge Case 2). The reference database lookup is cached in Redis with a 5-minute TTL for sub-500ms response. Both endpoints are accessible to `Patient` and `Staff` roles since insurance entry occurs during the booking flow. All validation results are persisted in the `insurance_validation_results` table for staff audit.

---

## Dependent Tasks

- **us_037/task_003** — `insurance_providers` reference table, `insurance_validation_results` table, and `validation_status` enum migration must be applied.
- **us_009** — `InsuranceProfile` entity and `insurance_profiles` table must exist (foundational dependency).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `InsuranceController` | CREATE | New controller: `POST /api/v1/insurance/validate`, `POST /api/v1/insurance` |
| `IInsuranceValidationService` | CREATE | Interface for soft validation logic |
| `InsuranceValidationService` | CREATE | Business logic: format validation, provider lookup, duplicate detection, result categorisation |
| `IInsuranceProfileService` | CREATE | Interface for insurance profile CRUD |
| `InsuranceProfileService` | CREATE | Persistence logic: save profile with validation status |
| `InsuranceValidateRequest` DTO | CREATE | Request: `PolicyNumber`, `ProviderCode`, `ProviderName`, `GroupNumber`, `Tier`, `PatientId` |
| `InsuranceValidateResponse` DTO | CREATE | Response: `Status`, `Warnings[]`, `ProviderMatch`, `PolicyFormatValid` |
| `InsuranceSaveRequest` DTO | CREATE | Request: `PatientId`, `PolicyNumber`, `ProviderCode`, `ProviderName`, `GroupNumber`, `Tier`, `ValidationStatus`, `CardImageFrontPath?`, `CardImageBackPath?` |
| `InsuranceValidationRetryService` | CREATE | `BackgroundService` for retrying `ValidationPending` records |
| `InsuranceModule` DI registration | MODIFY | Register all services as scoped; register retry background service |

---

## Implementation Plan

1. **Create DTOs** in `Insurance/DTOs/`:
   - `InsuranceValidateRequest` with `[Required, MinLength(5), MaxLength(30)] string PolicyNumber`, `[Required, MaxLength(20)] string ProviderCode`, `[Required, MaxLength(100)] string ProviderName`, `[MaxLength(30)] string? GroupNumber`, `[Required] InsuranceTier Tier`, `[Required] Guid PatientId`.
   - `InsuranceValidateResponse` with `ValidationStatus Status`, `List<string> Warnings`, `bool ProviderMatch`, `bool PolicyFormatValid`.
   - `InsuranceSaveRequest` extending validate request with `ValidationStatus`, optional card image paths.
2. **Create `IInsuranceValidationService`** interface with `ValidateAsync(InsuranceValidateRequest request, CancellationToken ct)` returning `Task<InsuranceValidateResponse>`.
3. **Implement `InsuranceValidationService.ValidateAsync`**:
   - **Provider lookup**: Query `insurance_providers` table for `ProviderCode` match. Cache the full provider list in Redis (`insurance:providers:all`, 5-min TTL) for fast lookup. If provider not found, add warning "Unknown provider code".
   - **Policy format validation**: If provider found, apply the `policy_number_pattern` regex from the provider record against `PolicyNumber`. If no match, add warning "Policy number format does not match expected pattern for provider".
   - **Duplicate detection**: If request includes secondary insurance context (passed via optional `PrimaryPolicyNumber` field), compare policy numbers. If identical, add warning "Potential duplicate policy number".
   - **Result categorisation**: If zero warnings → `SoftValidated`. If warnings exist but provider found → return warnings with `SoftValidated` status (soft warnings per AC-2). If provider not found AND format invalid → `ValidationFailed`.
   - **Reference DB fallback**: Wrap the provider lookup in a try-catch. If the DB query throws (connection failure, timeout), set status to `ValidationPending`, add warning "Validation deferred — reference database unavailable", and return immediately.
   - **Persist validation result**: Write to `insurance_validation_results` table with request details, result status, warnings JSON, and timestamp.
4. **Implement `InsuranceProfileService.SaveAsync`**:
   - Create or update the `InsuranceProfile` entity with `PatientId`, `PolicyNumber`, `ProviderName`, `Tier`, `ValidationStatus`, and card image paths.
   - Return the persisted profile with its ID.
5. **Create `InsuranceController`** at route `api/v1/insurance`:
   - `POST /validate` accepts `[FromBody] InsuranceValidateRequest`. Returns `InsuranceValidateResponse`. Accessible to `[Authorize(Roles = "Patient,Staff")]`.
   - `POST /` accepts `[FromBody] InsuranceSaveRequest`. Returns the persisted `InsuranceProfile`. Accessible to `[Authorize(Roles = "Patient,Staff")]`.
6. **Implement `InsuranceValidationRetryService`** as a `BackgroundService`:
   - Periodically (every 5 minutes) query `insurance_validation_results` where `status = 'ValidationPending'` and `retry_count < 3`.
   - Re-execute validation logic for each pending record. Update status to `SoftValidated` or `ValidationFailed` on success. Increment `retry_count` on continued failure.
   - Use `IServiceScopeFactory` to create scoped service instances within the background loop.

---

## Current Project State

```
Server/
├── Modules/
│   ├── Insurance/                                     ← CREATE (this task)
│   │   ├── Controllers/
│   │   │   └── InsuranceController.cs                 ← CREATE
│   │   ├── Services/
│   │   │   ├── IInsuranceValidationService.cs         ← CREATE
│   │   │   ├── InsuranceValidationService.cs          ← CREATE
│   │   │   ├── IInsuranceProfileService.cs            ← CREATE
│   │   │   ├── InsuranceProfileService.cs             ← CREATE
│   │   │   └── InsuranceValidationRetryService.cs     ← CREATE
│   │   └── DTOs/
│   │       ├── InsuranceValidateRequest.cs            ← CREATE
│   │       ├── InsuranceValidateResponse.cs           ← CREATE
│   │       └── InsuranceSaveRequest.cs                ← CREATE
│   └── [existing modules...]
├── Program.cs                                          ← MODIFY (DI registration)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual module structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Modules/Insurance/Controllers/InsuranceController.cs` | `POST /api/v1/insurance/validate` and `POST /api/v1/insurance` with `[Authorize(Roles="Patient,Staff")]` |
| CREATE | `Server/Modules/Insurance/Services/IInsuranceValidationService.cs` | Interface for soft validation |
| CREATE | `Server/Modules/Insurance/Services/InsuranceValidationService.cs` | Format validation, provider lookup with Redis cache, duplicate detection, result categorisation, fallback handling |
| CREATE | `Server/Modules/Insurance/Services/IInsuranceProfileService.cs` | Interface for insurance profile CRUD |
| CREATE | `Server/Modules/Insurance/Services/InsuranceProfileService.cs` | EF Core persistence for insurance profiles |
| CREATE | `Server/Modules/Insurance/Services/InsuranceValidationRetryService.cs` | `BackgroundService` for retrying `ValidationPending` records |
| CREATE | `Server/Modules/Insurance/DTOs/InsuranceValidateRequest.cs` | Request DTO with `[Required]`, `[MinLength]`, `[MaxLength]` validation |
| CREATE | `Server/Modules/Insurance/DTOs/InsuranceValidateResponse.cs` | Response DTO with status, warnings, match flags |
| CREATE | `Server/Modules/Insurance/DTOs/InsuranceSaveRequest.cs` | Save request DTO with validation status and card image paths |
| MODIFY | `Server/Program.cs` | Register `IInsuranceValidationService`, `IInsuranceProfileService` as scoped; register `InsuranceValidationRetryService` as hosted service |

---

## External References

- ASP.NET Core 8 `BackgroundService`: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0
- ASP.NET Core 8 `IDistributedCache` with Redis: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0
- EF Core 8 query filtering: https://learn.microsoft.com/en-us/ef/core/querying/
- .NET `Regex` for policy number pattern matching: https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions
- ASP.NET Core 8 role-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0
- FR-IP-001: System MUST perform insurance soft validation against formatting and reference records without blocking booking completion
- NFR-002: API response within 500 ms p95 — enforced by Redis-cached provider lookup (5-min TTL)
- NFR-007: Encrypt protected health information at rest using AES-256

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

- [ ] Unit tests pass for `InsuranceValidationService.ValidateAsync` (mock EF context, mock `IDistributedCache`)
- [ ] Unit tests pass for `InsuranceProfileService.SaveAsync` (mock EF context)
- [ ] Integration tests pass for `POST /api/v1/insurance/validate` returning `SoftValidated` on valid input
- [ ] Integration tests pass for `POST /api/v1/insurance/validate` returning warnings on format mismatch
- [ ] Integration tests pass for `POST /api/v1/insurance/validate` returning `ValidationFailed` on complete failure
- [ ] Integration tests pass for `POST /api/v1/insurance/validate` returning `ValidationPending` when reference DB is unavailable
- [ ] Authorization verified: unauthenticated → `401`; unauthorized role → `403`
- [ ] Validation response returned within 500 ms (AC-1, NFR-002)
- [ ] Background retry service processes `ValidationPending` records correctly

---

## Implementation Checklist

- [ ] Create request/response DTOs with validation attributes (`[Required]`, `[MinLength]`, `[MaxLength]`)
- [ ] Implement `InsuranceValidationService.ValidateAsync` with provider lookup (Redis-cached, 5-min TTL) and policy format regex validation
- [ ] Implement duplicate policy number detection between primary and secondary insurance
- [ ] Implement graceful fallback: catch reference DB failures, return `ValidationPending`, persist for retry
- [ ] Implement `InsuranceProfileService.SaveAsync` persisting insurance profile with validation status
- [ ] Create `InsuranceController` with `[Authorize(Roles = "Patient,Staff")]` for validate and save endpoints
- [ ] Implement `InsuranceValidationRetryService` as `BackgroundService` retrying pending validations (max 3 retries, 5-min interval)
- [ ] Persist all validation results to `insurance_validation_results` table for staff audit

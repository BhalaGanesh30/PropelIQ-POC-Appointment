---
task_id: task_003
user_story: us_038
epic: EP-005
layer: Database
status: not-started
effort_hours: 2
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_038] Secure Insurance Data Storage
- **Story Location**: [.propel/context/tasks/EP-005/us_038/us_038.md](.propel/context/tasks/EP-005/us_038/us_038.md)
- **Acceptance Criteria**:
  - AC-1: All insurance fields are encrypted using AES-256 before storage — requires encrypted field columns to store ciphertext.
  - AC-2: Encrypted data is decrypted transparently using the correct key — requires `key_version` column for key rotation support.
- **Edge Cases**:
  - Edge Case 1: Encryption key is rotated — `key_version` column enables the application to select the correct decryption key per record.
  - Edge Case 2: Missing card images — `card_image_front_key` and `card_image_back_key` are nullable.

---

## Design References (Database Task)

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

Extend the `insurance_profiles` table schema to support AES-256 field-level encryption and Cloudflare R2 card image storage references. This migration adds encrypted field columns (`encrypted_policy_number`, `encrypted_provider_name`, `encrypted_group_number`) storing Base64-encoded ciphertext alongside HMAC digests, a `key_version` integer column for encryption key rotation tracking (Edge Case 1), and `card_image_front_key` / `card_image_back_key` nullable columns storing R2 object keys for card images (Edge Case 2 — nullable for optional images). The original plaintext columns (`policy_number`, `provider_name`) are retained temporarily during migration but marked for removal in a subsequent migration after data migration is complete. All schema changes are delivered as an EF Core 8 migration with rollback support per DR-007.

---

## Dependent Tasks

- **us_037/task_003** — `insurance_profiles` table with `validation_status` column must exist. This task extends it with encryption and image columns.
- **us_009** — `insurance_profiles` table must exist (foundational dependency).

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `insurance_profiles.encrypted_policy_number` | ADD COLUMN | TEXT NOT NULL DEFAULT '' — Base64-encoded AES-256 ciphertext |
| `insurance_profiles.encrypted_provider_name` | ADD COLUMN | TEXT NOT NULL DEFAULT '' — Base64-encoded AES-256 ciphertext |
| `insurance_profiles.encrypted_group_number` | ADD COLUMN | TEXT — nullable, encrypted group number |
| `insurance_profiles.policy_number_hmac` | ADD COLUMN | VARCHAR(64) — HMAC-SHA256 for tamper detection |
| `insurance_profiles.provider_name_hmac` | ADD COLUMN | VARCHAR(64) — HMAC-SHA256 for tamper detection |
| `insurance_profiles.key_version` | ADD COLUMN | INT NOT NULL DEFAULT 1 — encryption key version for rotation |
| `insurance_profiles.card_image_front_key` | ADD COLUMN | VARCHAR(255) — nullable R2 object key |
| `insurance_profiles.card_image_back_key` | ADD COLUMN | VARCHAR(255) — nullable R2 object key |
| `InsuranceProfile` EF entity | MODIFY | Add properties for all new columns |
| EF Core migration | CREATE | Migration file for all column additions |

---

## Implementation Plan

1. **Add encrypted field columns to `insurance_profiles`**:
   - `encrypted_policy_number` TEXT NOT NULL DEFAULT '' — stores Base64-encoded AES-256 ciphertext (IV + ciphertext).
   - `encrypted_provider_name` TEXT NOT NULL DEFAULT '' — stores Base64-encoded AES-256 ciphertext.
   - `encrypted_group_number` TEXT NULL — nullable, encrypted group number ciphertext.
   - `policy_number_hmac` VARCHAR(64) NOT NULL DEFAULT '' — HMAC-SHA256 digest for tamper detection on policy number.
   - `provider_name_hmac` VARCHAR(64) NOT NULL DEFAULT '' — HMAC-SHA256 digest for provider name.
   - DEFAULT '' on NOT NULL columns allows backward-compatible rollout — existing rows are valid without immediate data migration.
2. **Add `key_version` column**: `key_version` INT NOT NULL DEFAULT 1. This column tracks which encryption key was used for each record. During key rotation (Edge Case 1), the background service reads this value to determine which key to use for decryption before re-encrypting with the current key.
3. **Add card image R2 key columns**:
   - `card_image_front_key` VARCHAR(255) NULL — stores the Cloudflare R2 object key for the front card image. Nullable because card images are optional (Edge Case 2).
   - `card_image_back_key` VARCHAR(255) NULL — stores the R2 object key for the back card image. Nullable.
   - Replace the existing `card_image_path` column reference from models.md — the new columns use R2 object keys instead of local file paths.
4. **Add index on `key_version`**: B-tree index on `key_version` to support the key rotation background service querying records with `key_version < currentVersion`.
5. **Update `InsuranceProfile` EF entity**: Add C# properties for all new columns with appropriate EF Core configuration (column types, nullable settings, default values).
6. **Generate EF Core migration**: Run `dotnet ef migrations add AddInsuranceEncryptionColumns`. Verify `Up()` adds all columns and indexes. Verify `Down()` drops them cleanly per DR-007.

---

## Current Project State

```
Server/
├── Data/
│   ├── Entities/
│   │   ├── InsuranceProfile.cs                        ← MODIFY (add encrypted + image columns)
│   │   ├── InsuranceProvider.cs                       ← EXISTS (us_037/task_003)
│   │   ├── InsuranceValidationResult.cs               ← EXISTS (us_037/task_003)
│   │   └── [existing entities...]
│   ├── Migrations/
│   │   ├── YYYYMMDDHHMMSS_AddInsuranceValidationSchema.cs ← EXISTS (us_037/task_003)
│   │   └── YYYYMMDDHHMMSS_AddInsuranceEncryptionColumns.cs ← CREATE
│   └── AppDbContext.cs                                ← MODIFY (update InsuranceProfile mapping)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual entity folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Data/Migrations/YYYYMMDDHHMMSS_AddInsuranceEncryptionColumns.cs` | Migration: add encrypted columns, HMAC columns, key_version, card image keys, index |
| MODIFY | `Server/Data/Entities/InsuranceProfile.cs` | Add properties for encrypted fields, HMACs, key version, card image keys |
| MODIFY | `Server/Data/AppDbContext.cs` | Update `InsuranceProfile` Fluent API configuration for new columns |

---

## External References

- EF Core 8 migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- EF Core 8 column configuration: https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties
- PostgreSQL TEXT type for variable-length ciphertext: https://www.postgresql.org/docs/15/datatype-character.html
- DR-001: Globally unique identifiers (UUID) for core entities
- DR-002: Referential integrity and transactional consistency
- DR-007: Schema migration with backward-compatible, zero-downtime rollouts
- NFR-007: Encrypt protected health information at rest using AES-256

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddInsuranceEncryptionColumns --project Server/Server.csproj

# Apply migration
dotnet ef database update --project Server/Server.csproj

# Rollback migration
dotnet ef database update AddInsuranceValidationSchema --project Server/Server.csproj

# Run tests
dotnet test
```

---

## Implementation Validation Strategy

- [ ] Migration applies cleanly on a database with existing `insurance_profiles` table (from us_037/task_003)
- [ ] Migration rolls back cleanly via `Down()` method
- [ ] All encrypted columns added with correct types and defaults
- [ ] `key_version` column defaults to 1 for existing rows
- [ ] `card_image_front_key` and `card_image_back_key` are nullable
- [ ] B-tree index on `key_version` created successfully
- [ ] Existing data in `insurance_profiles` is preserved after migration

---

## Implementation Checklist

- [ ] Add `encrypted_policy_number`, `encrypted_provider_name`, `encrypted_group_number` TEXT columns with backward-compatible defaults
- [ ] Add `policy_number_hmac` and `provider_name_hmac` VARCHAR(64) columns for tamper detection
- [ ] Add `key_version` INT column (default 1) with B-tree index for key rotation queries
- [ ] Add nullable `card_image_front_key` and `card_image_back_key` VARCHAR(255) columns for R2 object keys
- [ ] Update `InsuranceProfile` EF entity with properties for all new columns
- [ ] Generate and verify EF Core migration with working `Up()` and `Down()` methods (DR-007)

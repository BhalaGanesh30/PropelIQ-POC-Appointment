---
task_id: task_003
user_story: us_037
epic: EP-005
layer: Database
status: not-started
effort_hours: 3
---

# Task - task_003

## Requirement Reference

- **User Story**: [us_037] Insurance Soft Validation Engine
- **Story Location**: [.propel/context/tasks/EP-005/us_037/us_037.md](.propel/context/tasks/EP-005/us_037/us_037.md)
- **Acceptance Criteria**:
  - AC-1: System validates the policy number format and provider code against the reference database — requires `insurance_providers` reference table with format patterns.
  - AC-3: Record is saved with a `SoftValidated` status flag — requires `validation_status` enum on `insurance_profiles`.
  - AC-4: System flags the record with `ValidationFailed` status and records the validation result — requires `insurance_validation_results` table.
- **Edge Cases**:
  - Edge Case 1: Reference database unavailable — insurance record saved with `ValidationPending` status; background retry queued — requires `retry_count` and `validation_status` columns.

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

Create the database schema extensions required by the insurance soft validation engine. This task adds: (1) an `insurance_providers` reference table containing provider codes, names, and policy number format regex patterns used by the validation engine for format checking (AC-1); (2) an `insurance_validation_results` table to persist every validation attempt with status, warnings, and retry metadata for staff audit (AC-4) and background retry processing (Edge Case 1); and (3) extends the existing `insurance_profiles` table (from US_009) with a `validation_status` enum column supporting `SoftValidated`, `ValidationFailed`, and `ValidationPending` states (AC-3, AC-4). The migration also seeds the `insurance_providers` table with common provider format patterns. All schema changes are delivered as an EF Core 8 migration with rollback support per DR-007.

---

## Dependent Tasks

- **us_009** — `insurance_profiles` table must exist (foundational dependency). This task extends it with the `validation_status` column.

---

## Impacted Components

| Component / Module | Action | Notes |
|--------------------|--------|-------|
| `insurance_providers` table | CREATE | Reference table: `provider_id`, `provider_code`, `provider_name`, `policy_number_pattern`, `is_active` |
| `insurance_validation_results` table | CREATE | Audit table: `validation_id`, `patient_id`, `policy_number`, `provider_code`, `status`, `warnings_json`, `retry_count`, `created_at`, `updated_at` |
| `insurance_profiles.validation_status` column | MODIFY | Add `validation_status` enum column (`SoftValidated`, `ValidationFailed`, `ValidationPending`) to existing table |
| `InsuranceProvider` EF entity | CREATE | Entity mapping for `insurance_providers` table |
| `InsuranceValidationResult` EF entity | CREATE | Entity mapping for `insurance_validation_results` table |
| `InsuranceProfile` EF entity | MODIFY | Add `ValidationStatus` enum property |
| `InsuranceDbContext` configuration | MODIFY | Add `DbSet<InsuranceProvider>`, `DbSet<InsuranceValidationResult>`, update `InsuranceProfile` mapping |
| EF Core migration | CREATE | Migration file for all schema changes |

---

## Implementation Plan

1. **Create `validation_status` enum type**: Define PostgreSQL enum `validation_status_enum` with values `SoftValidated`, `ValidationFailed`, `ValidationPending`. Map to C# `ValidationStatus` enum via EF Core `HasConversion` or `HasPostgresEnum`.
2. **Create `insurance_providers` reference table**:
   - Columns: `provider_id` (UUID PK, default `gen_random_uuid()`), `provider_code` (VARCHAR(20) UNIQUE NOT NULL), `provider_name` (VARCHAR(100) NOT NULL), `policy_number_pattern` (VARCHAR(255) NOT NULL — regex pattern), `is_active` (BOOLEAN NOT NULL DEFAULT TRUE), `created_at` (TIMESTAMPTZ NOT NULL DEFAULT NOW()), `updated_at` (TIMESTAMPTZ).
   - Indexes: UNIQUE index on `provider_code` for fast lookup.
3. **Create `insurance_validation_results` table**:
   - Columns: `validation_id` (UUID PK, default `gen_random_uuid()`), `patient_id` (UUID FK → `patients.patient_id` NOT NULL), `policy_number` (VARCHAR(30) NOT NULL), `provider_code` (VARCHAR(20) NOT NULL), `status` (`validation_status_enum` NOT NULL), `warnings_json` (JSONB — array of warning strings), `retry_count` (INT NOT NULL DEFAULT 0), `created_at` (TIMESTAMPTZ NOT NULL DEFAULT NOW()), `updated_at` (TIMESTAMPTZ).
   - Indexes: B-tree index on `(patient_id, created_at DESC)` for patient validation history lookup. B-tree index on `(status)` filtered to `ValidationPending` for background retry queries (`WHERE status = 'ValidationPending' AND retry_count < 3`).
4. **Extend `insurance_profiles` table**: Add column `validation_status` (`validation_status_enum` NOT NULL DEFAULT `ValidationPending`). Add B-tree index on `validation_status` for staff review filtering.
5. **Seed reference data**: Insert common insurance provider entries into `insurance_providers`:
   - Example: `{ provider_code: 'BCBS', provider_name: 'Blue Cross Blue Shield', policy_number_pattern: '^[A-Z]{3}[0-9]{9}$' }`
   - Example: `{ provider_code: 'AETNA', provider_name: 'Aetna', policy_number_pattern: '^W[0-9]{8,12}$' }`
   - Example: `{ provider_code: 'UHC', provider_name: 'UnitedHealthcare', policy_number_pattern: '^[0-9]{9,11}$' }`
   - These are dummy reference patterns for development/testing per EP-005 key deliverables.
6. **Generate EF Core migration**: Run `dotnet ef migrations add AddInsuranceValidationSchema` and verify the generated migration includes all table creations, column additions, indexes, and seed data. Ensure the `Down()` method properly rolls back all changes per DR-007.

---

## Current Project State

```
Server/
├── Data/
│   ├── Entities/
│   │   ├── InsuranceProfile.cs                        ← MODIFY (add ValidationStatus)
│   │   ├── InsuranceProvider.cs                       ← CREATE
│   │   ├── InsuranceValidationResult.cs               ← CREATE
│   │   └── [existing entities...]
│   ├── Migrations/
│   │   └── YYYYMMDDHHMMSS_AddInsuranceValidationSchema.cs ← CREATE
│   └── AppDbContext.cs                                ← MODIFY (add DbSets, enum mapping)
└── [existing structure...]
```

> Placeholder: Update this tree after dependent tasks are complete and the actual entity folder structure is confirmed.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `Server/Data/Entities/InsuranceProvider.cs` | EF entity for `insurance_providers` reference table |
| CREATE | `Server/Data/Entities/InsuranceValidationResult.cs` | EF entity for `insurance_validation_results` audit table |
| CREATE | `Server/Data/Migrations/YYYYMMDDHHMMSS_AddInsuranceValidationSchema.cs` | Migration: create tables, add column, indexes, seed data, with rollback |
| MODIFY | `Server/Data/Entities/InsuranceProfile.cs` | Add `ValidationStatus` enum property |
| MODIFY | `Server/Data/AppDbContext.cs` | Add `DbSet<InsuranceProvider>`, `DbSet<InsuranceValidationResult>`, configure enum mapping, seed data |

---

## External References

- EF Core 8 migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- EF Core 8 enum conversions: https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions
- Npgsql EF Core PostgreSQL enums: https://www.npgsql.org/efcore/mapping/enum.html
- PostgreSQL JSONB columns: https://www.postgresql.org/docs/15/datatype-json.html
- PostgreSQL partial indexes: https://www.postgresql.org/docs/15/indexes-partial.html
- DR-001: Globally unique identifiers (UUID) for core entities
- DR-002: Referential integrity and transactional consistency
- DR-007: Schema migration with backward-compatible, versioned scripts with rollback

---

## Build Commands

```bash
# Generate migration
dotnet ef migrations add AddInsuranceValidationSchema --project Server/Server.csproj

# Apply migration
dotnet ef database update --project Server/Server.csproj

# Rollback migration
dotnet ef database update <PreviousMigrationName> --project Server/Server.csproj

# Run tests
dotnet test
```

---

## Implementation Validation Strategy

- [ ] Migration applies cleanly on a fresh database
- [ ] Migration rolls back cleanly via `Down()` method
- [ ] `insurance_providers` table created with correct columns, types, and UNIQUE constraint on `provider_code`
- [ ] `insurance_validation_results` table created with FK to `patients`, JSONB `warnings_json`, and indexes
- [ ] `insurance_profiles.validation_status` column added with correct enum type and default value
- [ ] Seed data inserted: at least 3 reference providers with valid regex patterns
- [ ] Partial index on `insurance_validation_results` filters `ValidationPending` records efficiently

---

## Implementation Checklist

- [ ] Define `ValidationStatus` enum (`SoftValidated`, `ValidationFailed`, `ValidationPending`) and configure PostgreSQL enum mapping
- [ ] Create `insurance_providers` reference table with `provider_code` UNIQUE index and `policy_number_pattern` regex column
- [ ] Create `insurance_validation_results` audit table with JSONB warnings, FK to patients, and composite/partial indexes
- [ ] Add `validation_status` column to existing `insurance_profiles` table with default `ValidationPending`
- [ ] Seed `insurance_providers` with dummy reference providers (BCBS, Aetna, UHC) per EP-005 deliverables
- [ ] Generate and verify EF Core migration with working `Up()` and `Down()` methods (DR-007)

# Task - TASK_001

## Requirement Reference

- User Story: us_012
- Story Location: .propel/context/tasks/EP-DATA/us_012/us_012.md
- Acceptance Criteria:
  - AC-1: Given row-level security or column-level policies are applied, When the API service role queries Patient records, Then only columns permitted for the application role are returned; restricted PHI columns are excluded or masked.
  - AC-2: Given an analytics or reporting role is configured, When it queries clinical data, Then direct identifiers are not accessible, and only aggregate or de-identified views are returned.
  - AC-4: Given a database user attempts to query a PHI column without the required role, When the query executes, Then the database returns an access denied error and the attempt is logged.
- Edge Case:
  - How does the system handle multi-tenancy isolation for operational data? (Tenant-level row filters are applied so cross-tenant data leaks are prevented at the query level.)

## Design References (Frontend Tasks Only)

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

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | N/A | N/A |
| Backend | ASP.NET Core Web API | 8.x |
| Database | PostgreSQL with pgvector | 15.x |
| Library | Npgsql | latest stable |
| Library | pgaudit | latest stable |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| AI Gateway | N/A | N/A |
| Mobile | N/A | N/A |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Implement PostgreSQL row-level security (RLS) policies and column-level GRANT restrictions that enforce role-aware data access for PHI columns. Three database roles are created — `app_api` (application service), `app_analytics` (reporting/analytics), and `app_admin` (administrative) — each with precisely scoped column-level privileges on PHI-bearing tables. RLS policies enforce tenant-level row isolation so that cross-tenant data leaks are prevented at the query level. A de-identified analytics view (`vw_patients_deidentified`) masks direct identifiers for the analytics role. The `pgaudit` extension logs all access denied attempts for compliance. All policies are delivered as an EF Core migration with raw SQL blocks.

## Dependent Tasks

- US_009 task_001 (requires domain entity models and schema to be in place)
- US_010 task_001 (requires audit table immutability, pgaudit extension)

## Impacted Components

- New: EF Core migration `AddPhiAccessPolicies` (RLS policies, column grants, roles, views)
- New: `infra/postgres/phi_policy_baseline.sql` (canonical baseline of all PHI column permissions)
- Modify: `infra/postgres/init.sql` (create database roles `app_api`, `app_analytics`, `app_admin`)
- Modify: PostgreSQL connection string configuration (role-specific connection strings in `appsettings`)

## Implementation Plan

1. **Create database roles** in `infra/postgres/init.sql` with least-privilege defaults. Each role maps to a specific application concern per DR-008 and TR-006:

```sql
-- Application service role (API layer)
CREATE ROLE app_api WITH LOGIN PASSWORD 'api_pass' NOINHERIT;
COMMENT ON ROLE app_api IS 'API service role — permitted PHI columns for direct patient care';

-- Analytics/reporting role (de-identified access only)
CREATE ROLE app_analytics WITH LOGIN PASSWORD 'analytics_pass' NOINHERIT;
COMMENT ON ROLE app_analytics IS 'Analytics role — no direct PHI access, de-identified views only';

-- Administrative role (full PHI access, audited)
CREATE ROLE app_admin WITH LOGIN PASSWORD 'admin_pass' NOINHERIT;
COMMENT ON ROLE app_admin IS 'Admin role — full PHI access with pgaudit logging';

-- Grant schema usage to all roles
GRANT USAGE ON SCHEMA app TO app_api, app_analytics, app_admin;
```

2. **Define PHI column classification** — identify all columns containing protected health information across entity tables. PHI columns per HIPAA Safe Harbor:

| Table | PHI Columns | Non-PHI Columns |
|-------|-------------|-----------------|
| `app.patients` | `first_name`, `last_name`, `date_of_birth`, `email`, `phone`, `address`, `mrn`, `contact_preferences` | `id`, `created_at`, `updated_at`, `tenant_id` |
| `app.clinical_documents` | `document_content`, `file_path`, `patient_notes` | `id`, `patient_id`, `document_type`, `created_at`, `updated_at`, `tenant_id` |
| `app.clinical_facts` | `extracted_value`, `source_text` | `id`, `document_id`, `field_name`, `confidence_score`, `verification_state`, `created_at`, `tenant_id` |
| `app.insurance_profiles` | `policy_number`, `group_number`, `subscriber_name` | `id`, `patient_id`, `provider_name`, `plan_type`, `created_at`, `tenant_id` |

3. **Apply column-level GRANT restrictions** (AC-1). The `app_api` role receives SELECT on all columns needed for patient care workflows but NOT on raw document content. The `app_analytics` role receives SELECT only on non-PHI columns:

```sql
-- === app_api role: permitted PHI columns for direct patient care ===
GRANT SELECT, INSERT, UPDATE ON app.patients TO app_api;
-- Explicitly grant column-level SELECT (AC-1: only permitted columns returned)
GRANT SELECT (id, first_name, last_name, date_of_birth, email, phone,
              mrn, contact_preferences, created_at, updated_at, tenant_id)
    ON app.patients TO app_api;

GRANT SELECT (id, patient_id, document_type, created_at, updated_at, tenant_id)
    ON app.clinical_documents TO app_api;
-- Exclude document_content, file_path, patient_notes from app_api

GRANT SELECT ON app.clinical_facts TO app_api;
GRANT SELECT ON app.appointments TO app_api;
GRANT INSERT ON app.audit_records TO app_api;

-- === app_analytics role: NO direct PHI access ===
GRANT SELECT (id, created_at, updated_at, tenant_id)
    ON app.patients TO app_analytics;
-- No PHI columns granted — analytics must use de-identified view

GRANT SELECT (id, patient_id, document_type, created_at, updated_at, tenant_id)
    ON app.clinical_documents TO app_analytics;

GRANT SELECT (id, document_id, field_name, confidence_score, verification_state, created_at, tenant_id)
    ON app.clinical_facts TO app_analytics;

-- === app_admin role: full access (audited via pgaudit) ===
GRANT ALL ON ALL TABLES IN SCHEMA app TO app_admin;
```

4. **Enable RLS and create tenant isolation policies** (edge case: multi-tenancy). Every query is filtered by `tenant_id` matching the session variable `app.current_tenant_id`:

```sql
-- Enable RLS on all tenant-bearing tables
ALTER TABLE app.patients ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.appointments ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_facts ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.insurance_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.waitlist_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.reminder_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE app.coding_decisions ENABLE ROW LEVEL SECURITY;

-- Tenant isolation policy template (applied to each table)
-- Uses session variable set by the API middleware: SET app.current_tenant_id = '<uuid>'
CREATE POLICY tenant_isolation_patients ON app.patients
    AS PERMISSIVE
    FOR ALL
    TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation_appointments ON app.appointments
    AS PERMISSIVE
    FOR ALL
    TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation_clinical_documents ON app.clinical_documents
    AS PERMISSIVE
    FOR ALL
    TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation_clinical_facts ON app.clinical_facts
    AS PERMISSIVE
    FOR ALL
    TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation_insurance ON app.insurance_profiles
    AS PERMISSIVE
    FOR ALL
    TO app_api, app_analytics
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

-- Admin bypasses RLS (table owner or superuser)
CREATE POLICY admin_bypass_patients ON app.patients
    AS PERMISSIVE FOR ALL TO app_admin USING (true);

-- Force RLS even for table owners (defense in depth)
ALTER TABLE app.patients FORCE ROW LEVEL SECURITY;
ALTER TABLE app.appointments FORCE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_documents FORCE ROW LEVEL SECURITY;
ALTER TABLE app.clinical_facts FORCE ROW LEVEL SECURITY;
ALTER TABLE app.insurance_profiles FORCE ROW LEVEL SECURITY;
ALTER TABLE app.waitlist_entries FORCE ROW LEVEL SECURITY;
ALTER TABLE app.reminder_events FORCE ROW LEVEL SECURITY;
ALTER TABLE app.coding_decisions FORCE ROW LEVEL SECURITY;
```

5. **Create de-identified analytics view** (AC-2). The `app_analytics` role accesses clinical data only through this view, which strips direct identifiers and returns masked/hashed values:

```sql
CREATE OR REPLACE VIEW app.vw_patients_deidentified AS
SELECT
    p.id,
    -- Hash direct identifiers (one-way, non-reversible)
    encode(sha256(convert_to(p.first_name || p.last_name, 'UTF8')), 'hex')
        AS name_hash,
    date_part('year', age(p.date_of_birth))::int AS age_years,
    -- Mask email: show domain only
    '***@' || split_part(p.email, '@', 2) AS email_domain,
    p.tenant_id,
    p.created_at
FROM app.patients p;

GRANT SELECT ON app.vw_patients_deidentified TO app_analytics;
REVOKE SELECT ON app.patients FROM app_analytics;
-- Analytics role can only access the de-identified view
```

6. **Configure pgaudit for access-denied logging** (AC-4). Log all failed access attempts so that unauthorized PHI queries are captured for compliance per NFR-007 and NFR-010:

```sql
-- Enable pgaudit logging for the relevant roles
ALTER SYSTEM SET pgaudit.log = 'all';
ALTER SYSTEM SET pgaudit.log_catalog = 'off';
ALTER SYSTEM SET pgaudit.log_level = 'log';
ALTER SYSTEM SET pgaudit.log_statement_once = 'on';

-- Role-specific audit configuration
ALTER ROLE app_api SET pgaudit.log = 'write, ddl';
ALTER ROLE app_analytics SET pgaudit.log = 'read, ddl';
ALTER ROLE app_admin SET pgaudit.log = 'all';
```

PostgreSQL natively returns `permission denied` errors when a role queries a column it lacks GRANT on. The `pgaudit` extension logs the statement that caused the error, satisfying AC-4.

7. **Set tenant context in API middleware**. The ASP.NET Core middleware sets the PostgreSQL session variable before each request so RLS policies filter correctly:

```csharp
// In TenantContextMiddleware.cs (conceptual — middleware sets session var)
// Called on each request after authentication
public class TenantContextMiddleware
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "SET app.current_tenant_id = {0}", tenantId);
        }
        await _next(context);
    }
}
```

8. **Create PHI policy baseline file** at `infra/postgres/phi_policy_baseline.sql` — a canonical snapshot of all PHI column assignments and role permissions used by the CI drift check (task_002):

```sql
-- PHI Policy Baseline — Canonical permission snapshot
-- Used by CI/CD drift detection (task_002)
-- Last updated: [migration timestamp]

-- Format: TABLE | COLUMN | app_api | app_analytics | app_admin
-- Values: GRANT / DENY / N/A

-- app.patients
-- id             | GRANT | GRANT | GRANT
-- first_name     | GRANT | DENY  | GRANT
-- last_name      | GRANT | DENY  | GRANT
-- date_of_birth  | GRANT | DENY  | GRANT
-- email          | GRANT | DENY  | GRANT
-- phone          | GRANT | DENY  | GRANT
-- mrn            | GRANT | DENY  | GRANT
-- address        | DENY  | DENY  | GRANT
-- contact_preferences | GRANT | DENY | GRANT
-- tenant_id      | GRANT | GRANT | GRANT

-- [... all tables documented ...]
```

## Current Project State

```text
propelIQ/
├── docker-compose.yml       (from US_005)
├── .env.example
├── infra/
│   ├── postgres/
│   │   └── init.sql         (from US_003)
│   ├── backup/              (from US_011)
│   └── ...
└── server/
    └── src/
        ├── PropelIQ.Api/
        ├── PropelIQ.Domain/         (entities from US_009)
        ├── PropelIQ.Infrastructure/  (DbContext, migrations from US_009)
        └── PropelIQ.Application/
```

> Placeholder: Update on execution based on US_009 and US_010 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | EF Core migration `AddPhiAccessPolicies` | RLS policies, column grants, roles, de-identified view, pgaudit config |
| CREATE | infra/postgres/phi_policy_baseline.sql | Canonical baseline of PHI column permissions per role |
| MODIFY | infra/postgres/init.sql | Add `app_api`, `app_analytics`, `app_admin` role creation with NOINHERIT |
| MODIFY | server/src/PropelIQ.Api/Middleware/TenantContextMiddleware.cs | Set `app.current_tenant_id` session variable for RLS |
| MODIFY | server/src/PropelIQ.Api/appsettings.json | Add role-specific connection strings for api, analytics, admin |
| MODIFY | .env.example | Add `APP_API_DB_PASSWORD`, `APP_ANALYTICS_DB_PASSWORD`, `APP_ADMIN_DB_PASSWORD` |

## External References

- PostgreSQL row-level security: https://www.postgresql.org/docs/15/ddl-rowsecurity.html
- PostgreSQL CREATE POLICY: https://www.postgresql.org/docs/15/sql-createpolicy.html
- PostgreSQL column-level privileges: https://www.postgresql.org/docs/15/sql-grant.html
- PostgreSQL current_setting for session variables: https://www.postgresql.org/docs/15/functions-admin.html#FUNCTIONS-ADMIN-SET
- pgaudit extension: https://www.pgaudit.org/
- HIPAA Safe Harbor de-identification: https://www.hhs.gov/hipaa/for-professionals/privacy/special-topics/de-identification/index.html
- EF Core raw SQL migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing?tabs=dotnet-core-cli#adding-raw-sql

## Build Commands

```bash
# Generate migration
cd server/src/PropelIQ.Infrastructure
dotnet ef migrations add AddPhiAccessPolicies --startup-project ../PropelIQ.Api

# Apply migration
dotnet ef database update --startup-project ../PropelIQ.Api

# Verify roles exist
docker exec propeliq-postgres psql -U postgres -d propeliq -c "\du"

# Test column access as app_api
docker exec propeliq-postgres psql -U app_api -d propeliq \
  -c "SELECT first_name, last_name FROM app.patients LIMIT 1;"

# Test column denial as app_analytics (should fail)
docker exec propeliq-postgres psql -U app_analytics -d propeliq \
  -c "SELECT first_name FROM app.patients LIMIT 1;"

# Verify de-identified view
docker exec propeliq-postgres psql -U app_analytics -d propeliq \
  -c "SELECT * FROM app.vw_patients_deidentified LIMIT 5;"

# Check pgaudit logs for denied access
docker compose logs postgres | grep "AUDIT"

# Test RLS tenant isolation
docker exec propeliq-postgres psql -U app_api -d propeliq \
  -c "SET app.current_tenant_id = '00000000-0000-0000-0000-000000000001'; SELECT count(*) FROM app.patients;"
```

## Implementation Validation Strategy

- [ ] `app_api` role can SELECT permitted PHI columns on `app.patients` (AC-1)
- [ ] `app_api` role is denied access to `address` and document content columns
- [ ] `app_analytics` role cannot SELECT any PHI column directly from `app.patients` (AC-2)
- [ ] `app_analytics` role can query `vw_patients_deidentified` and receives only hashed/masked data (AC-2)
- [ ] Unauthorized column access returns `permission denied` error from PostgreSQL (AC-4)
- [ ] `pgaudit` logs capture the denied query statement and role name (AC-4)
- [ ] RLS policies filter rows by `tenant_id` — cross-tenant queries return zero rows (edge case)
- [ ] `TenantContextMiddleware` sets `app.current_tenant_id` session variable per request

## Implementation Checklist

- [x] Create `app_api`, `app_analytics`, `app_admin` roles in `infra/postgres/init.sql` with `NOINHERIT` and schema USAGE grants
- [x] Apply column-level GRANT restrictions per PHI classification table — `app_api` gets care-relevant PHI, `app_analytics` gets non-PHI only
- [x] Enable RLS on all tenant-bearing tables and create `tenant_isolation_*` policies using `current_setting('app.current_tenant_id')`
- [x] Create `vw_patients_deidentified` view with SHA-256 name hash, age derivation, and email domain masking
- [x] Configure `pgaudit` role-level logging (write+DDL for api, read+DDL for analytics, all for admin)
- [x] Create `infra/postgres/phi_policy_baseline.sql` documenting all column-role permission assignments
- [x] Implement `TenantContextMiddleware` that sets PostgreSQL session variable from JWT `tenant_id` claim
- [x] Add role-specific connection strings to `appsettings.json` and environment variables to `.env.example`

# Task - TASK_001

## Requirement Reference

- User Story: us_010
- Story Location: .propel/context/tasks/EP-DATA/us_010/us_010.md
- Acceptance Criteria:
  - AC-3: Given the AuditRecord table is created, When a database role attempts to execute UPDATE or DELETE on the AuditRecord table, Then the operation is rejected by a write-restriction policy or row-level security rule.
  - AC-4: Given a new audit event is written, When the INSERT succeeds, Then the record is immutable and its content matches the structured schema defined for audit events.
- Edge Case:
  - How does the system handle attempts to bypass audit restrictions via DBA access? Audit table access is logged at the connection level; privileged access requires documented change-control approval.

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
| Backend | N/A | N/A |
| Database | PostgreSQL with pgvector | 15.x |
| Library | EF Core (migration runner) | 8.x |
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

Enforce append-only immutability on the `audit_records` table at the PostgreSQL database layer using a combination of trigger functions, restricted GRANT privileges, and connection-level audit logging via `pgaudit`. A `BEFORE UPDATE OR DELETE` trigger on `app.audit_records` raises an exception to block any mutation attempt, regardless of which database role executes it. The application role (`app_user`) is granted only `INSERT` and `SELECT` on the audit table — never `UPDATE` or `DELETE`. For DBA-level access bypass detection (edge case), `pgaudit` extension logs all DDL and DML statements on the audit schema, providing connection-level traceability per NFR-010. All database objects are delivered as an EF Core migration so they are version-controlled and applied via `dotnet ef database update`.

## Dependent Tasks

- US_009 task_001 (requires AuditRecord entity and table schema defined)
- US_009 task_002 (requires InitialSchema migration applied)
- US_003 task_001 (requires PostgreSQL container running with init scripts)

## Impacted Components

- New: `server/src/SharedServices.Infrastructure/Persistence/Migrations/<timestamp>_AuditTableImmutability.cs` (EF Core migration with raw SQL)
- New: Database trigger function `app.fn_prevent_audit_mutation()`
- New: Database trigger `app.trg_audit_records_immutable`
- Modify: PostgreSQL init script or migration: `GRANT INSERT, SELECT ON app.audit_records TO app_user`
- New: `pgaudit` extension configuration in PostgreSQL init script

## Implementation Plan

1. **Create EF Core migration** `AuditTableImmutability` containing raw SQL for all database-layer enforcement objects. Using `migrationBuilder.Sql()` ensures these objects are version-controlled alongside the schema:

```bash
dotnet ef migrations add AuditTableImmutability \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations
```

2. **Create the `BEFORE UPDATE OR DELETE` trigger function** that prevents any mutation on `audit_records`. This is the primary enforcement mechanism for AC-3 and DR-005:

```sql
-- Trigger function: reject UPDATE and DELETE on audit_records
CREATE OR REPLACE FUNCTION app.fn_prevent_audit_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'Audit records are immutable. % operations are prohibited on app.audit_records. '
        'Ref: DR-005, NFR-010.',
        TG_OP;
END;
$$ LANGUAGE plpgsql;

-- Trigger: fires BEFORE any UPDATE or DELETE attempt
CREATE TRIGGER trg_audit_records_immutable
    BEFORE UPDATE OR DELETE ON app.audit_records
    FOR EACH ROW
    EXECUTE FUNCTION app.fn_prevent_audit_mutation();
```

The trigger fires `BEFORE` the operation so the mutation is rejected without touching the row. The exception message includes the operation type and references the governing requirements.

3. **Restrict GRANT privileges** for the application database role. The `app_user` role used by the API receives only `INSERT` and `SELECT` — never `UPDATE`, `DELETE`, or `TRUNCATE`:

```sql
-- Revoke any existing broad privileges
REVOKE ALL ON app.audit_records FROM app_user;

-- Grant only INSERT (for writing events) and SELECT (for reading)
GRANT INSERT, SELECT ON app.audit_records TO app_user;
```

This creates a two-layer defense: (1) privilege restriction prevents the role from attempting mutation, and (2) the trigger catches any mutation attempt by privileged roles.

4. **Add `IMMUTABLE` column default for `occurred_at`** to ensure the timestamp is always server-generated and cannot be overridden by the application:

```sql
-- Ensure occurred_at is always set by the database server
ALTER TABLE app.audit_records
    ALTER COLUMN occurred_at SET DEFAULT now();
```

5. **Configure `pgaudit` extension** for connection-level DBA access logging (edge case). Add the extension to the PostgreSQL init script and configure it to log all DDL and privileged DML on the audit schema:

```sql
-- Enable pgaudit extension
CREATE EXTENSION IF NOT EXISTS pgaudit;

-- Configure pgaudit to log all DDL and role-based access
ALTER SYSTEM SET pgaudit.log = 'ddl, role';
ALTER SYSTEM SET pgaudit.log_catalog = off;
ALTER SYSTEM SET pgaudit.log_relation = on;
ALTER SYSTEM SET pgaudit.log_statement_once = on;
SELECT pg_reload_conf();
```

Additionally, add `shared_preload_libraries = 'pgaudit'` to the PostgreSQL Docker Compose command:

```yaml
# In docker-compose.yml postgres service
command: >
  postgres
  -c shared_preload_libraries=pgaudit
  -c pgaudit.log=ddl,role
  -c pgaudit.log_relation=on
```

This satisfies the edge case: DBA access attempts are captured in PostgreSQL server logs with connection details, user identity, and the SQL statement.

6. **Implement the migration `Down()` method** to cleanly rollback all objects:

```sql
-- Down migration: remove in reverse order
DROP TRIGGER IF EXISTS trg_audit_records_immutable ON app.audit_records;
DROP FUNCTION IF EXISTS app.fn_prevent_audit_mutation();
REVOKE ALL ON app.audit_records FROM app_user;
GRANT ALL ON app.audit_records TO app_user;  -- Restore default
```

7. **Add validation query** to the migration that verifies the trigger is active after apply:

```sql
-- Post-apply validation (runs as part of migration)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger
        WHERE tgname = 'trg_audit_records_immutable'
    ) THEN
        RAISE EXCEPTION 'Migration verification failed: trg_audit_records_immutable trigger not found.';
    END IF;
END $$;
```

## Current Project State

```text
propelIQ/
├── server/
│   ├── PropelIQ.sln
│   └── src/
│       └── SharedServices.Infrastructure/
│           └── Persistence/
│               ├── AppDbContext.cs
│               ├── Migrations/
│               │   ├── <timestamp>_InitialSchema.cs   (from US_009)
│               │   └── AppDbContextModelSnapshot.cs
│               └── Seed/
│                   └── AppDbContextSeed.cs
├── docker-compose.yml
├── infra/
│   └── postgres/
│       └── init.sql   (from US_003)
└── .env.example
```

> Placeholder: Update on execution based on US_009 task completion state.

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/src/SharedServices.Infrastructure/Persistence/Migrations/\<timestamp\>_AuditTableImmutability.cs | EF Core migration with trigger function, trigger, GRANT restrictions, and pgaudit config |
| MODIFY | docker-compose.yml | Add `shared_preload_libraries=pgaudit` to postgres service command args |
| MODIFY | infra/postgres/init.sql | Add `CREATE EXTENSION IF NOT EXISTS pgaudit` and pgaudit configuration |

## External References

- PostgreSQL PL/pgSQL trigger functions: https://www.postgresql.org/docs/15/plpgsql-trigger.html
- PostgreSQL CREATE TRIGGER: https://www.postgresql.org/docs/15/sql-createtrigger.html
- PostgreSQL GRANT statement: https://www.postgresql.org/docs/15/sql-grant.html
- PostgreSQL REVOKE statement: https://www.postgresql.org/docs/15/sql-revoke.html
- pgaudit extension: https://www.pgaudit.org/
- PostgreSQL shared_preload_libraries: https://www.postgresql.org/docs/15/runtime-config-client.html#GUC-SHARED-PRELOAD-LIBRARIES
- EF Core raw SQL in migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations
- DR-005 (append-only audit): .propel/context/docs/design.md
- NFR-010 (immutable audit evidence): .propel/context/docs/design.md

## Build Commands

```bash
# Generate migration (empty shell — SQL added manually)
dotnet ef migrations add AuditTableImmutability \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api \
  --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api

# Verify trigger exists
docker exec propeliq-postgres psql -U app_user -d propeliq -c \
  "SELECT tgname FROM pg_trigger WHERE tgrelid = 'app.audit_records'::regclass;"

# Test immutability (should fail with exception)
docker exec propeliq-postgres psql -U app_user -d propeliq -c \
  "UPDATE app.audit_records SET event_type = 'tampered' WHERE id = (SELECT id FROM app.audit_records LIMIT 1);"

# Rollback migration
dotnet ef database update <PreviousMigrationName> \
  --project server/src/SharedServices.Infrastructure \
  --startup-project server/src/PropelIQ.Api
```

## Implementation Validation Strategy

- [ ] `UPDATE` on `app.audit_records` raises exception with descriptive error mentioning DR-005 (AC-3)
- [ ] `DELETE` on `app.audit_records` raises exception with descriptive error (AC-3)
- [ ] `INSERT` on `app.audit_records` succeeds and record content matches structured schema (AC-4)
- [ ] `app_user` role has only `INSERT` and `SELECT` privileges on `audit_records`
- [ ] `pgaudit` logs DDL and role-based DML statements in PostgreSQL server logs (edge case)
- [ ] `occurred_at` column defaults to `now()` when not explicitly provided
- [ ] Migration rollback cleanly drops trigger and function without errors
- [ ] Trigger verification query confirms `trg_audit_records_immutable` exists after migration apply

## Implementation Checklist

- [x] Create EF Core migration `AuditTableImmutability` with `migrationBuilder.Sql()` for all raw SQL objects
- [x] Create `app.fn_prevent_audit_mutation()` trigger function that raises exception on UPDATE or DELETE
- [x] Create `trg_audit_records_immutable` BEFORE trigger on `app.audit_records` for UPDATE and DELETE
- [x] Configure GRANT: `INSERT, SELECT` only on `app.audit_records` for `app_user`; revoke UPDATE/DELETE/TRUNCATE
- [x] Set `occurred_at` column DEFAULT to `now()` for server-generated timestamps
- [x] Configure `pgaudit` extension: `shared_preload_libraries`, log DDL and role-based access
- [x] Implement `Down()` migration to drop trigger, function, and restore privileges
- [x] Add post-apply validation query confirming trigger exists on `audit_records` table

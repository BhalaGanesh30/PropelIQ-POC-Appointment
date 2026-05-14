# Bug Fix Task - user_type Column Missing During Staff Booking

## Bug Report Reference
- Bug ID: N/A (fallback folder used)
- Source: Runtime API error payload from staff booking UI (`POST /api/v1/staff-bookings`)

## Bug Summary

### Issue Classification
- **Priority**: High
- **Severity**: Core workflow broken for staff-assisted booking with inline patient creation
- **Affected Version**: Current local runtime process serving UI requests
- **Environment**: Windows, .NET 8 API, PostgreSQL, Angular UI

### Steps to Reproduce
1. Open staff booking wizard in UI.
2. Submit booking with inline `newPatient` payload.
3. **Expected**: Booking created and returned as `201`.
4. **Actual**: API returns `500` with Postgres `42703`.

**Error Output**:
```text
Npgsql.PostgresException: 42703: column "user_type" of relation "users" does not exist
```

### Root Cause Analysis
- **File**: `server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Infrastructure/StaffBooking/StaffBookingService.cs:147`
- **Component**: Scheduling.Infrastructure / StaffBooking
- **Function**: `CreateBookingAsync` -> `CreateInlinePatient`
- **Cause**: Inline staff booking creates `User` entity and EF model expects `app.users.user_type`. Database schema lacks this column because migrations are not fully applied.

Additional technical evidence:
- `user_type` is required by EF mapping in `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/Configurations/UserConfiguration.cs`.
- Migration that adds the column exists in `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Migrations/20260512083343_AddUserActivitySchema.cs`.
- Migrator fails before completion due ownership permissions (`must be owner of table appointments`), leaving drift unresolved.

Root-cause answers:
1. **Immediate trigger**: INSERT into `app.users` references missing `user_type` column.
2. **Underlying cause**: Schema drift from unapplied migrations and DB ownership mismatch.
3. **Why missed earlier**: No startup compatibility gate blocked API when schema/model were out of sync.

### Impact Assessment
- **Affected Features**: Staff-assisted booking (inline patient path), potentially any write path inserting/updating `User` with `UserType` mapping.
- **User Impact**: Staff cannot complete patient booking flow in UI.
- **Data Integrity Risk**: Yes (partial transaction attempts, user-facing failures).
- **Security Implications**: Medium operational risk (error payload leaks internals in development); no direct auth bypass.

## Fix Component Strategy

| Fix Component | Type | Rationale |
|---|---|---|
| Apply missing schema column (`user_type`) | Database migration/SQL | Resolves immediate 42703 write failure |
| Repair migration ownership/privileges | Config/DB admin | Enables normal migration pipeline for all pending changes |
| Add startup schema compatibility check | Code change | Prevents app from serving incompatible schema at runtime |
| Add regression integration test for inline patient booking | Test | Catches missing-column/schema drift impact on critical flow |
| Standardize local run profile and proxy target | Config | Avoids stale runtime/port mismatch masking real fix |

## Fix Overview
Primary remediation is schema alignment: ensure `app.users.user_type` exists and full migration pipeline can run under a DB owner-capable account. Secondary remediation introduces fail-fast startup checks and regression coverage.

## Fix Dependencies
- DB credentials with table-owner permissions for `app.*` objects.
- Controlled maintenance window for applying pending migrations.
- Access to API startup configuration for adding schema gate.

## Impacted Components
### Backend (.NET / EF Core / Npgsql)
- `server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Infrastructure/StaffBooking/StaffBookingService.cs` (validated call path)
- `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Data/Configurations/UserConfiguration.cs` (mapped `UserType`)
- `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Migrations/20260512083343_AddUserActivitySchema.cs` (DDL source)
- API startup bootstrapping (`Program.cs`) for schema compatibility gate

### Database
- `app.users` table
- migration history table `app.__ef_migrations_history`

### Frontend
- `app/proxy.conf.json` and runtime profile alignment (port consistency)

## Expected Changes
| Action | File Path | Description |
|---|---|---|
| MODIFY | server/src/PropelIQ.Api/Program.cs | Add startup check for pending migrations/schema compatibility and fail-fast logging |
| MODIFY | server/src/PropelIQ.DbMigrator/appsettings.json | Ensure correct default local credentials align with runtime profile |
| MODIFY | server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Infrastructure/StaffBooking/StaffBookingService.cs | Remove temporary bypasses once schema fully fixed; keep code path clean |
| CREATE | server/tests/PropelIQ.Api.Tests/StaffBooking/StaffBookingSchemaCompatibilityTests.cs | Integration test for inline new patient booking flow against migrated schema |
| CREATE | db hotfix script (ops-controlled) | One-time SQL fallback to add `app.users.user_type` if migration blocked |

## Implementation Plan
1. Validate current runtime port and process used by UI; stop stale API process serving old binaries.
2. Run DB hotfix under owner-capable account:
   - `ALTER TABLE app.users ADD COLUMN IF NOT EXISTS user_type varchar(50) NOT NULL DEFAULT 'Staff';`
3. Fix DB ownership/permissions so migrator can apply pending migrations end-to-end.
4. Re-run migrator using correct credentials and verify `20260512083343_AddUserActivitySchema` is recorded.
5. Add startup schema gate:
   - Check `Database.GetPendingMigrations()` and critical column presence (`app.users.user_type`).
   - Fail startup with explicit operator message if incompatible.
6. Remove temporary runtime bypasses after schema correction and re-enable intended audit path.
7. Add integration test for staff booking with `newPatient` payload and assert non-500 response path.
8. Validate UI end-to-end through proxied API target.

## Regression Prevention Strategy
- [ ] Integration test: `POST /api/v1/staff-bookings` with `newPatient` succeeds when schema is current.
- [ ] Startup gate test: app fails fast when pending migrations exist in non-dev environments.
- [ ] DB migration smoke check in CI/CD before deployment.
- [ ] Port/process consistency check in local runbook.

## Rollback Procedure
1. If migration/hotfix causes unexpected behavior, revert DB changes from backup snapshot and redeploy last-known-good API image.
2. Temporarily re-enable guarded bypass for non-critical audit writes only if business continuity requires it.
3. Re-run booking smoke tests and verify no data corruption in `app.users`, `app.patients`, `app.appointments`.

## External References
- EF Core docs: apply migrations programmatically and CLI migration update (`MigrateAsync`, `dotnet ef database update`) from `/dotnet/entityframework.docs`.
- EF Core docs: pending model changes check (`dotnet ef migrations has-pending-model-changes`).

## Build Commands
```powershell
cd d:\IQ\server
dotnet build src/PropelIQ.Api/PropelIQ.Api.csproj -c Release -v minimal

$env:DATABASE_URL="Host=localhost;Port=5432;Database=propeliq;Username=propeliq_user;Password=admin;Search Path=app"
dotnet run --project src/PropelIQ.DbMigrator/PropelIQ.DbMigrator.csproj
```

## Implementation Validation Strategy
- [ ] Reproduced bug before fix (500 with 42703 user_type missing)
- [ ] API returns non-500 for same payload after schema fix
- [ ] `app.users` contains `user_type` column and valid default value behavior
- [ ] Staff booking E2E succeeds in UI through proxied API target
- [ ] No new failures in related user creation flows

## Implementation Checklist
- [ ] Apply DB hotfix (if needed) under owner account
- [ ] Resolve DB ownership permissions
- [ ] Apply pending migrations successfully
- [ ] Add startup schema compatibility guard
- [ ] Add regression integration test
- [ ] Verify UI booking flow end-to-end

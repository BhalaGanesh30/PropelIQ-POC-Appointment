# Bug Fix Task - bug_queue_empty_dashboard

## Bug Report Reference
- Bug ID: bug_queue_empty_dashboard
- Source: Developer-reported — `http://localhost:4200/queue` shows empty table

## Bug Summary

### Issue Classification
- **Priority**: High
- **Severity**: Core feature non-functional in dev environment
- **Affected Version**: EP-003 (post-migration, local dev)
- **Environment**: Windows, local dev, IIS Express + Docker PostgreSQL

### Steps to Reproduce
1. Start IIS Express (run API from Visual Studio)
2. Run Angular dev server (`ng s` in `app/`)
3. Log in as admin (`admin@propeliq.local`)
4. Navigate to `http://localhost:4200/queue`
5. **Expected**: Queue Dashboard shows 8 upcoming appointments with risk badges
6. **Actual**: Table is empty, spinner resolves to empty state

**Error Output**:
```text
No visible error. API call to GET /api/v1/appointments/risk-scores?from=...&to=...
returns HTTP 200 with body: []
```

### Root Cause Analysis

**Root Cause 1 — Missing EF Migrations (Primary)**
- **Files**: 
  - `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Migrations/20260427000000_AddSequenceNumberToAppointments.cs`
  - `server/src/Modules/SharedServices/PropelIQ.Modules.SharedServices.Infrastructure/Migrations/20260428111743_AddWaitlistClaimTokenHash.cs`
- **Component**: AppDbContext / PostgreSQL schema
- **Cause**: Two EP-003 migrations were never applied to the local dev database. The `AddWaitlistClaimTokenHash` migration adds `risk_level`, `risk_confidence`, `risk_features`, `risk_scored_at` columns to `app.appointments`. Without these columns, `GetUpcomingForRiskDashboardAsync` would fail and `INoShowRiskScoringService.ScoreAsync` has no columns to read/write.
- **Why not caught**: DbMigrator was not run after EP-003 changes; IIS Express locks DLLs preventing `dotnet ef database update` from running alongside the running app.

**Root Cause 2 — No Appointment Seed Data**
- **File**: N/A (missing seed data)
- **Component**: `app.appointments` table
- **Cause**: `app.appointments` contained 0 rows. Even after applying migrations, `BookingRepository.GetUpcomingForRiskDashboardAsync` returns an empty list. The `AppDbContextSeed` only creates one mock patient with no appointments.
- **Why not caught**: No dev data seeding strategy was defined for the scheduling module in EP-003.

### Impact Assessment
- **Affected Features**: Queue Dashboard (`/queue`), risk score API (`GET /api/v1/appointments/risk-scores`), high-risk alert banner
- **User Impact**: Staff/Admin users see an empty queue dashboard — entire EP-003 US_028 feature unverifiable in dev
- **Data Integrity Risk**: No — schema change is additive; existing data unaffected
- **Security Implications**: None

## Fix Overview
Two sequential fixes:
1. Apply the two pending EF migrations directly via SQL (bypassing `dotnet ef` build lock caused by IIS Express)
2. Insert seed appointment and patient data covering the next 7 days with varied risk levels (High/Medium/Low/Unknown)

## Fix Dependencies
- PostgreSQL running (`docker-compose up -d postgres`)
- Admin psql access (`-U postgres`)

## Impacted Components
### Database (Applied directly)
- `app.appointments` — 4 new columns added (`risk_level`, `risk_confidence`, `risk_features`, `risk_scored_at`), composite index added
- `app.waitlist_entries` — 10 new columns, column renamed (`priority` → `preferred_duration_minutes`)
- `app.reminder_events` — 2 new columns (`idempotency_key`, `scheduled_at`)
- `app.dead_letter_events` — new table created
- `app.slot_templates` — new table created
- `app.__ef_migrations_history` — 2 rows inserted

### Database (Seed Data)
- `app.users` — 5 seed patient users inserted
- `app.patients` — 5 seed patients inserted
- `app.appointments` — 8 seed appointments inserted (next 7 days, varied risk)

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| APPLY SQL | `app.appointments` | Add risk_level/risk_confidence/risk_features/risk_scored_at/sequence_number columns |
| APPLY SQL | `app.waitlist_entries` | Add claim/offer lifecycle columns, rename priority |
| APPLY SQL | `app.reminder_events` | Add idempotency_key, scheduled_at |
| CREATE TABLE | `app.dead_letter_events` | New table for failed reminder events |
| INSERT DATA | `app.users` / `app.patients` / `app.appointments` | 5 patients + 8 upcoming appointments |

## Implementation Plan
1. Verify both migrations are not yet applied: `SELECT migration_id FROM app.__ef_migrations_history`
2. Apply migration SQL directly (see Build Commands below)
3. Seed appointment data
4. Restart IIS Express to pick up schema changes (if API was started before migration)
5. Re-login to get a fresh JWT with `Admin` role
6. Navigate to `http://localhost:4200/queue` — table should show 8 rows

## Regression Prevention Strategy
- [ ] Add `DbMigrator` run step to dev setup documentation / README
- [ ] Add `AppointmentSeed` class to `AppDbContextSeed` with at least 5 future appointments
- [ ] Add integration test asserting `GetUpcomingForRiskDashboardAsync` returns non-empty result when appointments exist

## Rollback Procedure
1. To remove seed data: `DELETE FROM app.appointments WHERE confirmation_code IS NULL AND booked_at < now() - interval '1 minute'`
2. To reverse migrations: Run the `Down()` methods from both migration files as SQL (drop added columns/tables)

## External References
- `server/src/PropelIQ.Api/Controllers/RiskScoreController.cs` — `GET api/v1/appointments/risk-scores`
- `server/src/Modules/Scheduling/PropelIQ.Modules.Scheduling.Infrastructure/Booking/BookingRepository.cs:225` — `GetUpcomingForRiskDashboardAsync`
- `app/src/app/features/queue/queue-dashboard.component.ts` — frontend date window (now → now+7d)

## Build Commands

### Apply Migrations (IIS Express running — use SQL file)
```powershell
$env:PGPASSWORD = "admin"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d propeliq -f "d:\IQ\apply_migrations.sql"
```

### Seed Appointments
```powershell
$env:PGPASSWORD = "admin"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d propeliq -f "d:\IQ\seed_appointments.sql"
```

### Standard (when IIS Express is stopped)
```powershell
cd d:\IQ\server
dotnet ef database update `
  --project src\Modules\SharedServices\PropelIQ.Modules.SharedServices.Infrastructure\PropelIQ.Modules.SharedServices.Infrastructure.csproj `
  --startup-project src\PropelIQ.Api\PropelIQ.Api.csproj
```

## Implementation Validation Strategy
- [ ] `SELECT COUNT(*) FROM app.appointments` returns 8
- [ ] `SELECT column_name FROM information_schema.columns WHERE table_schema='app' AND table_name='appointments' AND column_name='risk_level'` returns 1 row
- [ ] `GET /api/v1/appointments/risk-scores?from=<now>&to=<now+7d>` returns 8 items (with Admin JWT, no `Bearer` prefix in Swagger Authorize)
- [ ] `http://localhost:4200/queue` shows 8 rows in table
- [ ] High-risk alert banner shows 2 entries (Alice Johnson + Bob Martinez, scheduled within 24h)
- [ ] Risk badges display correct colors (High=red, Medium=orange, Low=green, Unknown=grey)

## Implementation Checklist
- [x] Identified root cause 1: missing migrations
- [x] Identified root cause 2: no seed appointment data
- [x] Applied `20260427000000_AddSequenceNumberToAppointments` via SQL
- [x] Applied `20260428111743_AddWaitlistClaimTokenHash` via SQL
- [x] Verified `risk_level` column now exists on `app.appointments`
- [x] Seeded 5 patients + 8 appointments (next 7 days, varied risk)
- [ ] Restart IIS Express to ensure API runtime picks up schema changes
- [ ] Verify queue dashboard shows data at `http://localhost:4200/queue`
- [ ] Add `AppointmentSeed` to `AppDbContextSeed` (prevents recurrence)

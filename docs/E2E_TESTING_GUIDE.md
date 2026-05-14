# PropelIQ — End-to-End Testing Guide

## Overview

This guide covers manual end-to-end testing of all implemented features across every epic. Follow the steps in order: infrastructure → migrations → backend → frontend → per-feature flows.

---

## Step 1 — Start Infrastructure

```powershell
cd D:\IQ
docker compose up -d postgres redis
docker compose ps   # wait until both show "healthy"
```

---

## Step 2 — Run Database Migrations

```powershell
cd D:\IQ\server
dotnet run --project src/PropelIQ.DbMigrator
```

---

## Step 3 — Start the API

```powershell
cd D:\IQ\server
dotnet run --project src/PropelIQ.Api --launch-profile Development
```

| Endpoint | URL |
|----------|-----|
| API base | `http://localhost:5000` |
| Swagger UI | `http://localhost:5000/swagger` |
| Health check | `http://localhost:5000/health` |

---

## Step 4 — Start the Angular Dev Server

```powershell
cd D:\IQ\app
ng serve
# App available at: http://localhost:4200
```

---

## Step 5 — Create Test Accounts

Register via the UI at `http://localhost:4200/auth/register` or via Swagger → **POST /api/v1/auth/register**.

### Required test users

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@test.com` | `Test@123456789!` |  -- done
| Clinician | `clinician@test.com` | `Test@123456789!` |  -- done
| Staff | `staff@test.com` | `Test@123456789!` | -- done
| Patient | `patient@test.com` | `Test@123456789!` | --done

### Email confirmation (dev shortcut)

After each registration, the API console prints:

```
[DEV] Email confirmation URL for <email>:
http://localhost:5000/api/v1/auth/confirm-email?userId=...&token=...
```

Open that URL in the browser to confirm the account.

> In development, login also auto-confirms unconfirmed accounts — watch the API logs.

### Assign roles via Swagger

After account creation, **only Patient role is assigned by default**. Staff/Clinician/Admin users need role assignment.

#### Option A: Bulk Role Assignment (Recommended)

1. **Login as Admin** → copy `accessToken`
2. **Click Authorize** → paste token
3. **GET /api/v1/admin/users** → search `clinician@test.com` → copy `userId`
4. **POST /api/v1/admin/users/bulk** with:
   ```json
   {
     "userIds": ["<userId-from-step-3>"],
     "action": 2,
     "targetRole": "Clinician"
   }
   ```
5. Repeat for Staff and other roles (action=2, targetRole=Staff/Admin)

Expected response: `{ "successCount": 1, "failureCount": 0 }`

#### Option B: Staff Invitation Flow (For Staff/Clinician/Admin roles)

Use **POST /api/v1/staffmanagement/invite** if you prefer to invite users with roles pre-assigned before they set passwords. This is the recommended production flow.

#### Role Transition Rules

- **Patient** → cannot be promoted to Staff/Clinician/Admin (blocked by transition validator)
- **Staff** ↔ **Clinician** (allowed)
- **Admin** can assign any role

If all 4 accounts were created via register (all Patient), only use the Invitation flow or manually update the database.

**Patients keep the default `Patient` role assigned at registration.**

---

## Step 6 — Flow-by-Flow Test Checklist

---

### Authentication (EP-001)

Login as each role at `http://localhost:4200/login`.

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| Login | `/login` | Submit credentials for each role | Redirected to `/dashboard` with correct nav items |
| Silent refresh | Any protected page | Leave open 15+ minutes | Token auto-refreshed; no logout |
| Logout | Nav header | Click logout | Redirected to `/login`; tokens cleared from storage |
| Invalid password | `/login` | Submit wrong password | Error message shown; lockout after 5 attempts |

---

### Scheduling (EP-002)

Login as **Patient** (`patient@test.com`).

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| Search slots | `/scheduling` | Search by date | Available slots grid appears |
| Book appointment | `/scheduling` | Select slot → confirm | Success toast; booking saved |
| View appointments | `/appointments` | Open page | Booked appointment listed with status |
| Reschedule | `/appointments` | Click reschedule | New slot picker opens; booking updated |
| Cancel | `/appointments` | Click cancel | Status changes to Cancelled; confirmation dialog shown |
| Join waitlist | `/waitlist` | Click join | Appears in waitlist view with position |
| Claim waitlist slot | `/waitlist` | Claim available slot | Slot reserved; removed from waitlist |

---

### Staff-Assisted Scheduling (EP-004)

Login as **Staff** (`staff@test.com`).

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| Walk-in registration | `/staff/walkin` | Register a walk-in patient | Patient added to queue |
| Booking wizard — Step 1 | `/staff/booking` | Search and select patient | Patient chip shows in Step 1 |
| Booking wizard — Step 2 | `/staff/booking` | Select an available slot | Conflict banner shown if overlap |
| Booking wizard — Step 3 | `/staff/booking` | Fill intake / override reason | Required when conflict acknowledged |
| Booking wizard — Step 4 | `/staff/booking` | Confirm booking | Success screen; booking attributed to staff actor |
| Daily schedule | `/staff/schedule` | View today | Appointment blocks render on time grid |
| Drag-and-drop | `/staff/schedule` | Drag a block to a new slot | Override reason dialog; booking updated |
| Real-time queue | `/staff/queue` | Open dashboard | Live status badges; auto-refreshes every 15 s |

---

### Insurance Verification (EP-005)

Login as **Staff** (`staff@test.com`).

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| Submit insurance form | `/insurance` | Fill patient insurance details | Soft-validation result shown (Advisory only — never blocks booking) |
| View report | `/staff/insurance/report` | Open report page | Records table with status chips |
| Filter by status | `/staff/insurance/report` | Select "Validation Failed" | Table filters within 500 ms |
| Export PDF | `/staff/insurance/report` | Click Export → PDF | PDF file downloaded |
| Export CSV | `/staff/insurance/report` | Click Export → CSV | CSV file downloaded |

---

### Document Management (EP-006)

Login as **Staff** (`staff@test.com`).

#### Upload

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| Upload PDF | `/documents/upload` | Drag-and-drop a PDF | Progress bar → ClamAV scan badge → OCR extraction status |
| Upload multiple | `/documents/upload` | Drop 3 files | Each file tracked independently in the queue |
| Invalid file type | `/documents/upload` | Drop a `.exe` | Validation error; file rejected before upload |
| Scan failure | `/documents/upload` | Upload a test EICAR file | Scan result badge shows "Infected"; file blocked |
| OCR completion | `/documents/upload` | Wait for processing | Extraction badge changes to "Completed"; preview text shown |
| Manual review flag | `/documents/upload` | Low-confidence OCR | "Manual review required" badge shown |

#### Library

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| View library | `/documents/library?patientId=<uuid>` | Open page | All non-deleted documents listed |
| Filter by category | Library | Select category dropdown | Table filters instantly |
| Filter by date | Library | Set date range | Documents filtered to range |
| Categorize | Library | Click category inline | Saved immediately without dialog |
| Rename | Library | Click rename | Dialog with current name; saves display name |
| Soft-delete | Library | Click delete → confirm | Document removed from active list |
| View trash | `/documents/trash` | Open page (Admin only) | Deleted documents shown with deletion date |
| Restore | Trash view | Click restore | Document returns to active library |

---

### Clinical Intelligence (EP-007)

Login as **Clinician** (`clinician@test.com`). Obtain a patient UUID from Swagger: **GET /api/v1/patients**.

#### 360° Patient Profile (US_045 — SCR-014)

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| Open profile | `/patients/<uuid>/profile` | Navigate | Patient header card loads (name, MRN, DOB) |
| Clinical Summary tab | Default tab | View | Medications, Allergies, Diagnoses, Findings category cards |
| Pagination | Summary tab | Click next page on any category | Facts update without full page reload |
| Empty profile | Profile of patient with no facts | View Summary | "No facts recorded" empty state per category |

#### Fact Editing (US_047 — FR-CA-004)

| Step | URL / Action | Expected Result |
|------|-------------|----------------|
| Edit a fact | Summary tab → click **Edit** on a fact | Inline edit form; prefilled with current name/value |
| Submit edit | Fill new value → submit | Fact updated; row_version incremented; ETag refreshed |
| Verify fact | Click **Verify** on a fact | Verified badge appears; verified_at timestamp set |
| Concurrent edit conflict | Edit same fact in two tabs simultaneously | Second submit returns HTTP 409; conflict message shown |
| View audit history | Click **History** icon on a fact | Chronological list of edits/verifications with editor name and timestamp |

#### Clinical Timeline (US_048 — SCR-015)

| Step | Tab / Action | Expected Result |
|------|-------------|----------------|
| Open Timeline tab | Click **Timeline** tab | Events load grouped by year; most-recent year expanded |
| Reverse chronological | View groups | Events within each year are most-recent-first |
| Filter: Medications | Select "Medications" chip | Only medication fact events shown |
| Filter: Documents | Select "Documents" chip | Only document upload events shown |
| Filter: All | Select "All" chip | All event types shown |
| Date range filter | Set From / To dates | Events filtered to range |
| Clear filter | Remove date range | All events return |
| Loading state | Throttle network | Skeleton cards shown during load |
| Empty state | Patient with no events | "No clinical events recorded yet" + Upload Documents CTA |
| Error state | Kill API → reload | Error banner with Retry button |
| Print | Click **Print Timeline** | All year panels expand; browser print dialog opens; panels restored after |
| Upload CTA | Empty state → click Upload | Navigates to documents section |

#### Conflict Alerts (US_046 — SCR-016)

| Step | Tab / Action | Expected Result |
|------|-------------|----------------|
| Open Conflicts tab | Click **Conflicts** tab | Alerts loaded; sorted Critical → High → Moderate → Low |
| Critical auto-dialog | Unacknowledged Critical alert exists | Mandatory acknowledgment dialog auto-opens on tab load |
| Acknowledge | Confirm dialog | Alert moves to Resolved (collapsed) section |
| Retry on error | Kill API → reload | Error banner with Retry button |
| Empty state | Patient with no conflicts | "No conflicts detected" empty state |

---

### Admin KPI Dashboard (EP-007)

Login as **Admin** (`admin@test.com` with role set to `Admin`).

| Step | URL | Action | Expected Result |
|------|-----|--------|----------------|
| KPI cards | `/admin/kpi` | Load page | Metric cards with headline values and line charts |
| Date range | KPI page | Change from/to dates | Charts reload within 1 second |
| Empty chart | Period with no data | View card | "No data for the selected period" annotation |
| Export PDF | Click export → PDF | PDF download triggered |
| Export CSV | Click export → CSV | CSV download triggered |

---

## Step 7 — API Smoke Tests via Swagger

Navigate to `http://localhost:5000/swagger`, click **Authorize**, and paste the JWT from the login response (`accessToken` field).

### Key endpoints

```
# Authentication
POST /api/v1/auth/login
POST /api/v1/auth/register
POST /api/v1/auth/refresh

# Patient Profile
GET  /api/v1/patients/{id}/profile
GET  /api/v1/patients/{id}/profile?tab=medications&limit=10&offset=0

# Clinical Timeline
GET  /api/v1/patients/{id}/timeline
GET  /api/v1/patients/{id}/timeline?category=Medications
GET  /api/v1/patients/{id}/timeline?category=Documents
GET  /api/v1/patients/{id}/timeline?dateFrom=2025-01-01&dateTo=2026-12-31
GET  /api/v1/patients/{id}/timeline?dateFrom=2026-01-01&dateTo=2025-01-01  → 400

# Fact Editing (requires If-Match header)
GET   /api/v1/clinical-facts/{factId}/history
PATCH /api/v1/clinical-facts/{factId}     Header: If-Match: "1"
POST  /api/v1/clinical-facts/{factId}/verify

# Conflict Alerts
GET  /api/v1/patients/{id}/conflicts

# Documents
POST /api/v1/documents/upload
GET  /api/v1/documents/{id}
GET  /api/v1/documents/{id}/content

# Health
GET  /health
GET  /health/ready
```

---

## Step 8 — Health Checks

```
http://localhost:5000/health         → "Healthy" when DB + Redis are up
http://localhost:5000/health/ready   → includes per-dependency status
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Login fails — "Email not confirmed" | Email not verified | Open the confirmation URL printed in the API console |
| 403 Forbidden on admin routes | User has wrong role | Login as Admin; use POST /api/v1/admin/users/bulk with action=2 to assign role |
| 422 Unprocessable Entity on role assign | Role transition not allowed | Patient cannot be promoted to Staff/Clinician/Admin. Use invite flow instead (POST /api/v1/staffmanagement/invite) |
| Timeline shows empty | No documents extracted yet | Upload a PDF and wait for OCR extraction to complete (`ExtractionStatus = Completed`) |
| Timeline returns 400 | `dateFrom > dateTo` | Ensure date range is chronologically valid |
| Redis connection error | Redis container not running | `docker compose up -d redis` and verify password in `appsettings.Development.json` |
| DB migration error | Schema out of sync | `docker compose down -v && docker compose up -d postgres` then re-run migrator |
| `ng serve` port conflict | Port 4200 in use | `ng serve --port 4201`; update `Cors:AllowedOrigin` in `appsettings.Development.json` |
| `@swimlane/ngx-charts` missing | `npm install` not run | `cd D:\IQ\app && npm install` |
| Optimistic concurrency 409 | Stale ETag | Reload the fact (GET profile) to get the latest `row_version`, then retry the PATCH |

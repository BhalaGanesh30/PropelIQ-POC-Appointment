# Bug Fix Task - bug_sidebar_role_nav

## Bug Report Reference
- Bug ID: bug_sidebar_role_nav
- Source: User report — "sidebar is same for patient and staff"

## Bug Summary

### Issue Classification
- **Priority**: High
- **Severity**: Core RBAC navigation broken for all Staff, Admin, and Clinician roles
- **Affected Version**: Current HEAD
- **Environment**: Angular 17.3.x, ASP.NET Core 8, PostgreSQL 16, Chrome/Edge (local dev)

### Steps to Reproduce
1. Log in as a user with role `Staff` (e.g. `sarah.mitchell@propeliq.com`)
2. Observe the sidebar navigation
3. **Expected**: Sidebar shows a "Staff" section with items: Real-Time Queue, Daily Schedule, Book for Patient, Walk-In, Risk Scores, Notifications
4. **Actual**: Sidebar shows only the patient-facing section (Dashboard, Find Appointment, My Appointments, My Waitlist) — identical to what a Patient user sees

**Secondary impact**: `roleGuard` also calls `getUserRole()`, so Staff/Admin routes (`/queue`, `/admin/users`, `/staff/booking`, etc.) redirect all Staff users to `/forbidden` instead of allowing access.

### Root Cause Analysis
- **File**: `app/src/app/core/services/token-storage.service.ts` — `getUserRole()` method
- **Component**: `TokenStorageService`
- **Function**: `getUserRole()`
- **Cause**:
  The backend (`JwtTokenService.cs`) adds the role claim using `new Claim(ClaimTypes.Role, role)`.
  `JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap` (active by default in .NET) maps
  `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) → `"role"` when
  serialising the JWT. So the actual JWT payload contains `"role": "Staff"` (short form).

  The frontend `getUserRole()` was reading:
  ```ts
  decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  ```
  This key does NOT exist in the JWT payload — it resolves to `undefined` / `null`.
  `null` role → `isStaff()` and `isAdmin()` computed signals return `false` → staff nav section
  never renders, and `roleGuard` denies access to all protected routes.

- **Why not caught earlier**: No unit test existed for `TokenStorageService.getUserRole()` using a
  real JWT payload. The claim key mismatch is a known .NET JWT integration trap when
  `DefaultOutboundClaimTypeMap` is active (the default).

### Impact Assessment
- **Affected Features**:
  - Sidebar navigation role-based sections (Staff, Admin)
  - `roleGuard` — blocks access to `/queue`, `/staff/*`, `/admin/*`, `/insurance/report`, `/templates`
  - `isStaff()` / `isAdmin()` computed signals in `MainLayoutComponent`
  - `userRole()` display in sidebar profile section
- **User Impact**: 100% of Staff, Admin, and Clinician users cannot access any role-protected route or see staff nav items
- **Data Integrity Risk**: No
- **Security Implications**: Inverse — users are being over-restricted (denied access they should have), not under-restricted

## Fix Overview

Check `decoded['role']` (the actual JWT short-form key) first in `getUserRole()`, with a fallback to the long-form URI for tokens generated with `DefaultOutboundClaimTypeMap` explicitly cleared.

## Fix Dependencies
- None — isolated to `TokenStorageService`

## Impacted Components
### Frontend (Angular)
- `app/src/app/core/services/token-storage.service.ts` — MODIFIED (already applied)

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `app/src/app/core/services/token-storage.service.ts` | `getUserRole()` reads `decoded['role']` first, falls back to URI form |
| CREATE | `app/src/app/core/services/token-storage.service.spec.ts` | Unit tests for `getUserRole()` with short-form and URI-form JWT payloads |

## Implementation Plan

### Step 1 — Fix `getUserRole()` [ALREADY APPLIED]
In `token-storage.service.ts`, replace the single URI-key lookup with a prioritised check:

```ts
getUserRole(): string | null {
  const decoded = this.getDecodedToken();
  if (!decoded) return null;
  // Primary: short claim name emitted by JwtSecurityTokenHandler DefaultOutboundClaimTypeMap.
  const shortRole = decoded['role'];
  if (shortRole != null) {
    return Array.isArray(shortRole) ? (shortRole[0] as string) : (shortRole as string);
  }
  // Fallback: full URI used when DefaultOutboundClaimTypeMap is cleared.
  const longRole =
    decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
  if (longRole != null) {
    return Array.isArray(longRole) ? (longRole[0] as string) : (longRole as string);
  }
  return null;
}
```

Status: ✅ Applied

### Step 2 — Add Unit Tests for `getUserRole()`
Create `app/src/app/core/services/token-storage.service.spec.ts`:

Test cases:
1. Returns `null` when no token is stored
2. Returns the role when JWT contains `"role": "Staff"` (short form — primary path)
3. Returns the role when JWT contains the long-form URI key (fallback path)
4. Returns first role when value is an array `"role": ["Staff", "Clinician"]`
5. Returns `null` when token payload has no role claim
6. `isAdmin()` in MainLayoutComponent returns `true` for role `"Admin"`
7. `isStaff()` in MainLayoutComponent returns `true` for roles `"Staff"`, `"Clinician"`

Helper — build a mock JWT (unsigned, for test use only):
```ts
function buildMockJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body   = btoa(JSON.stringify(payload));
  return `${header}.${body}.mock_signature`;
}
```

### Step 3 — Verify No Other Consumers Have the Same Bug
Check all other files that call `getDecodedToken()` directly and read role/claim keys.

Files to audit:
- `app/src/app/core/guards/role.guard.ts` — uses `getUserRole()` ✅ covered by fix
- `app/src/app/layouts/main-layout/main-layout.component.ts` — uses `getUserRole()` ✅ covered by fix

## Regression Prevention Strategy
- [ ] Unit test: `getUserRole()` returns correct role from `"role"` short-form JWT key
- [ ] Unit test: `getUserRole()` returns correct role from long-form URI JWT key (fallback)
- [ ] Unit test: `getUserRole()` returns null when no role claim present
- [ ] Unit test: `getUserRole()` handles array role values (multi-role tokens)
- [ ] Manual test: log in as Staff user → sidebar shows Staff section
- [ ] Manual test: log in as Admin user → sidebar shows both Staff and Admin sections
- [ ] Manual test: log in as Patient user → sidebar shows only patient nav items
- [ ] Manual test: Staff user can navigate to `/queue` without being redirected to `/forbidden`

## Rollback Procedure
1. Revert `getUserRole()` in `token-storage.service.ts` to the URI-form lookup (no data affected)
2. No migration or database rollback needed

## External References
- [JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap .NET docs](https://learn.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytokenhandler.defaultoutboundclaimtypemap)
- Angular signals `computed()` dependency tracking

## Build Commands
```bash
cd app
npx ng build --configuration development
npx ng test --include="**/token-storage.service.spec.ts"
```

## Implementation Validation Strategy
- [ ] Logged-in Staff user sees Staff section in sidebar
- [ ] `getUserRole()` returns `"Staff"` for a JWT with `"role": "Staff"`
- [ ] `roleGuard` allows Staff user to access `/queue`
- [ ] All existing passing tests still pass
- [ ] New unit tests pass

## Implementation Checklist
- [x] Fix `getUserRole()` to read `decoded['role']` first
- [ ] Create `token-storage.service.spec.ts` with full test suite
- [ ] Manually verify sidebar role sections after login as Staff and Admin
- [ ] Manually verify `/queue` and `/admin/*` routes accessible to correct roles

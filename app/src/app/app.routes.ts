import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { NotFoundComponent } from './features/not-found/not-found.component';
import { ForbiddenComponent } from './features/not-found/forbidden.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full',
  },
  {
    // Lazy-loaded feature: each child route is defined in features/dashboard/routes.ts.
    // authGuard protects all dashboard children; redirects to /login when unauthenticated.
    path: 'dashboard',
    loadChildren: () => import('./features/dashboard/routes'),
    canActivate: [authGuard],
  },
  {
    // Auth redirect target — no guard so unauthenticated users can always reach it.
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    // EP-001: authentication flow (registration, activate, future: password reset).
    path: 'auth',
    loadChildren: () => import('./features/auth/routes'),
  },
  {
    // EP-001/us_016: Admin staff management — requires authentication.
    path: 'admin',
    loadChildren: () => import('./features/admin/routes'),
    canActivate: [authGuard],
  },
  {
    // EP-002/us_019: Slot search and booking flow — requires authentication.
    path: 'scheduling',
    loadChildren: () => import('./features/scheduling/scheduling.routes'),
    canActivate: [authGuard],
  },
  {
    // EP-002/us_022: Appointment history with reschedule/cancel actions (SCR-007).
    path: 'appointments',
    loadComponent: () =>
      import('./features/appointments/appointment-history.component').then(
        (m) => m.AppointmentHistoryComponent,
      ),
    title: 'My Appointments — UPACIP',
    canActivate: [authGuard],
  },
  {
    // EP-002/us_023: Waitlist view with join, claim, and countdown timer (SCR-008).
    path: 'waitlist',
    loadComponent: () =>
      import('./features/waitlist/waitlist-view.component').then(
        (m) => m.WaitlistViewComponent,
      ),
    title: 'My Waitlist — UPACIP',
    canActivate: [authGuard],
  },
  {
    // US_028: Staff queue dashboard with AI no-show risk scoring (task_003).
    // Restricted to authenticated Staff/Admin users; role enforcement is also
    // applied server-side on the /api/v1/appointments/risk-scores endpoint.
    path: 'queue',
    loadComponent: () =>
      import('./features/queue/queue-dashboard.component').then(
        (m) => m.QueueDashboardComponent,
      ),
    title: 'Queue Dashboard — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // US_029/SCR-009: Patient notification channel and reminder timing preferences.
    path: 'settings/notifications',
    loadComponent: () =>
      import('./features/settings/notification-preferences.component').then(
        (m) => m.NotificationPreferencesComponent,
      ),
    title: 'Notification Preferences — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // US_057/AC-2: Patient disclosure request form and history (PatientOnly).
    path: 'settings/disclosure-requests',
    loadComponent: () =>
      import('./features/settings/disclosure/disclosure-request-form.component').then(
        (m) => m.DisclosureRequestFormComponent,
      ),
    title: 'Data Access Records — PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Patient'] },
  },
  {
    // US_030/task_002: Slot claim page — opened from HMAC-signed email/SMS link.
    // Accepts ?token= query param; authGuard redirects to login then back.
    path: 'claim',
    loadComponent: () =>
      import('./features/waitlist/slot-claim-page.component').then(
        (m) => m.SlotClaimPageComponent,
      ),
    title: 'Claim Appointment Slot — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-004/US_031: Real-time staff queue dashboard with status badges, wait-time
    // estimates, overdue highlighting, and 15 s polling (task_001_fe_queue_dashboard).
    path: 'staff/queue',
    loadComponent: () =>
      import('./features/queue/realtime-queue-dashboard.component').then(
        (m) => m.RealtimeQueueDashboardComponent,
      ),
    title: 'Real-Time Queue — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-005/US_037: Insurance Soft Validation Form (SCR-028).
    // Patients and staff submit insurance details; soft-validation result is
    // advisory only — booking is never blocked regardless of outcome (AC-2).
    path: 'insurance',
    loadComponent: () =>
      import('./features/insurance/insurance-validation-form.component').then(
        (m) => m.InsuranceValidationFormComponent,
      ),
    title: 'Insurance Verification — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-004/US_036: Daily Schedule Calendar with drag-and-drop rescheduling (SCR-026).
    // Renders a 7 AM – 7 PM time-grid with appointment blocks; staff can drag blocks to
    // reschedule (override reason dialog required) and print A4/Letter-formatted layout.
    path: 'staff/schedule',
    loadComponent: () =>
      import('./features/schedule/daily-schedule.component').then(
        (m) => m.DailyScheduleComponent,
      ),
    title: 'Daily Schedule — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-004/US_035: Staff-assisted booking wizard (SCR-027).
    // Staff selects a patient, picks a slot, provides intake/override reason, and confirms.
    // Restricted to authenticated Staff/Admin users; staffActorId is captured server-side.
    path: 'staff/booking',
    loadComponent: () =>
      import('./features/staff-booking/staff-booking-wizard.component').then(
        (m) => m.StaffBookingWizardComponent,
      ),
    title: 'Book for Patient — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-004/US_033: Walk-in patient registration and queue insertion (SCR-029).
    path: 'staff/walkin',
    loadComponent: () =>
      import('./features/walkin/walkin-registration.component').then(
        (m) => m.WalkinRegistrationComponent,
      ),
    title: 'Walk-In Registration — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-006/US_040: Document upload with malware scanning (SCR-011).
    // Accessible to both Patient and Staff roles — authGuard only (no role restriction).
    path: 'documents/upload',
    loadComponent: () =>
      import('./features/documents/document-upload.component').then(
        (m) => m.DocumentUploadComponent,
      ),
    title: 'Upload Documents — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-006/US_043: Document Library (SCR-012) — categorize, rename, soft-delete.
    // Accessible to Clinician and Staff roles; ?patientId= query param scopes the list.
    path: 'documents/library',
    loadComponent: () =>
      import('./features/documents/document-library.component').then(
        (m) => m.DocumentLibraryComponent,
      ),
    title: 'Document Library — PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Clinician', 'Staff'] },
  },
  {
    // EP-006/US_043 AC-4: Document Trash — restore soft-deleted documents (Admin only).
    // Reuses DocumentLibraryComponent; router activates the trash toggle via initialNavigation.
    path: 'documents/trash',
    loadComponent: () =>
      import('./features/documents/document-library.component').then(
        (m) => m.DocumentLibraryComponent,
      ),
    title: 'Document Trash — PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'], trashView: true },
  },
  {
    // EP-005/US_039: Staff-only insurance verification report (SCR-028 sub-view).
    // Role-guarded for Staff and Admin; Patients are redirected to /forbidden (Edge Case 2).
    path: 'staff/insurance/report',
    loadComponent: () =>
      import('./features/insurance/insurance-verification-report.component').then(
        (m) => m.InsuranceVerificationReportComponent,
      ),
    title: 'Insurance Verification Report — PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    // EP-007/US_045: 360° patient profile (SCR-014 / UXR-107).
    // Shows clinical summary (medications, allergies, diagnoses), timeline,
    // documents, insurance, and coding tabs for a given patient.
    path: 'patients/:id/profile',
    loadComponent: () =>
      import('./features/patients/patient-profile/patient-profile.component').then(
        (m) => m.PatientProfileComponent,
      ),
    title: 'Patient Profile — PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Clinician', 'Staff'] },
  },
  {
    // EP-008/US_052: Code search with autocomplete and favorites (SCR-018).
    // Clinician-only route; navigated to from SCR-017 suggestion cards and
    // the empty-state "Search Code" link when no AI suggestions are available.
    path: 'coding/search',
    loadComponent: () =>
      import('./features/coding/code-search/code-search.component').then(
        (m) => m.CodeSearchComponent,
      ),
    title: 'Code Search — PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Clinician'] },
  },
  {
    // 403 Forbidden — shown when an authenticated user lacks the required role.
    path: 'forbidden',
    component: ForbiddenComponent,
  },
  {
    // US_001 Edge Case: undefined routes render the 404 component (not a redirect,
    // preserves the URL so the user sees what they typed).
    path: '**',
    component: NotFoundComponent,
  },
];

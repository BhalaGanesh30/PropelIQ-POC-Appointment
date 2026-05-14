import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { ForbiddenComponent } from './features/not-found/forbidden.component';
import { NotFoundComponent } from './features/not-found/not-found.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full',
  },
  {
    path: 'dashboard',
    loadChildren: () => import('./features/dashboard/routes'),
    canActivate: [authGuard],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/routes'),
  },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/routes'),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    path: 'scheduling',
    loadChildren: () => import('./features/scheduling/scheduling.routes'),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Patient'] },
  },
  {
    path: 'appointments',
    loadComponent: () =>
      import('./features/appointments/appointment-history.component').then(
        (m) => m.AppointmentHistoryComponent,
      ),
    title: 'My Appointments - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Patient'] },
  },
  {
    path: 'waitlist',
    loadComponent: () =>
      import('./features/waitlist/waitlist-view.component').then(
        (m) => m.WaitlistViewComponent,
      ),
    title: 'My Waitlist - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Patient', 'Staff'] },
  },
  {
    path: 'queue',
    loadComponent: () =>
      import('./features/queue/queue-dashboard.component').then(
        (m) => m.QueueDashboardComponent,
      ),
    title: 'Queue Dashboard - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    path: 'settings/notifications',
    loadComponent: () =>
      import('./features/settings/notification-preferences.component').then(
        (m) => m.NotificationPreferencesComponent,
      ),
    title: 'Notification Preferences - PropelIQ',
    canActivate: [authGuard],
  },
  {
    path: 'settings/disclosure-requests',
    loadComponent: () =>
      import('./features/settings/disclosure/disclosure-request-form.component').then(
        (m) => m.DisclosureRequestFormComponent,
      ),
    title: 'Data Access Records - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Patient'] },
  },
  {
    path: 'claim',
    loadComponent: () =>
      import('./features/waitlist/slot-claim-page.component').then(
        (m) => m.SlotClaimPageComponent,
      ),
    title: 'Claim Appointment Slot - PropelIQ',
    canActivate: [authGuard],
  },
  {
    path: 'staff/queue',
    loadComponent: () =>
      import('./features/queue/realtime-queue-dashboard.component').then(
        (m) => m.RealtimeQueueDashboardComponent,
      ),
    title: 'Real-Time Queue - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    path: 'insurance',
    loadComponent: () =>
      import('./features/insurance/insurance-validation-form.component').then(
        (m) => m.InsuranceValidationFormComponent,
      ),
    title: 'Insurance Verification - PropelIQ',
    canActivate: [authGuard],
  },
  {
    path: 'staff/schedule',
    loadComponent: () =>
      import('./features/schedule/daily-schedule.component').then(
        (m) => m.DailyScheduleComponent,
      ),
    title: 'Daily Schedule - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    path: 'staff/booking',
    loadComponent: () =>
      import('./features/staff-booking/staff-booking-wizard.component').then(
        (m) => m.StaffBookingWizardComponent,
      ),
    title: 'Book for Patient - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    path: 'staff/walkin',
    loadComponent: () =>
      import('./features/walkin/walkin-registration.component').then(
        (m) => m.WalkinRegistrationComponent,
      ),
    title: 'Walk-In Registration - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    path: 'documents/upload',
    loadComponent: () =>
      import('./features/documents/document-upload.component').then(
        (m) => m.DocumentUploadComponent,
      ),
    title: 'Upload Documents - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Patient', 'Staff', 'Admin'] },
  },
  {
    path: 'documents/library',
    loadComponent: () =>
      import('./features/documents/document-library.component').then(
        (m) => m.DocumentLibraryComponent,
      ),
    title: 'Document Library - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Clinician', 'Staff'] },
  },
  {
    path: 'documents/trash',
    loadComponent: () =>
      import('./features/documents/document-library.component').then(
        (m) => m.DocumentLibraryComponent,
      ),
    title: 'Document Trash - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'], trashView: true },
  },
  {
    path: 'staff/insurance/report',
    loadComponent: () =>
      import('./features/insurance/insurance-verification-report.component').then(
        (m) => m.InsuranceVerificationReportComponent,
      ),
    title: 'Insurance Verification Report - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin'] },
  },
  {
    path: 'patients/:id/profile',
    loadComponent: () =>
      import('./features/patients/patient-profile/patient-profile.component').then(
        (m) => m.PatientProfileComponent,
      ),
    title: 'Patient Profile - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Clinician', 'Staff'] },
  },
  {
    path: 'coding/search',
    loadComponent: () =>
      import('./features/coding/code-search/code-search.component').then(
        (m) => m.CodeSearchComponent,
      ),
    title: 'Code Search - PropelIQ',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Clinician'] },
  },
  {
    path: 'forbidden',
    component: ForbiddenComponent,
  },
  {
    path: '**',
    component: NotFoundComponent,
  },
];
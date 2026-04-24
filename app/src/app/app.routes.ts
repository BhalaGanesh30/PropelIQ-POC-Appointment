import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { NotFoundComponent } from './features/not-found/not-found.component';

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
    title: 'My Appointments — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // EP-002/us_023: Waitlist view with join, claim, and countdown timer (SCR-008).
    path: 'waitlist',
    loadComponent: () =>
      import('./features/waitlist/waitlist-view.component').then(
        (m) => m.WaitlistViewComponent,
      ),
    title: 'My Waitlist — PropelIQ',
    canActivate: [authGuard],
  },
  {
    // US_001 Edge Case: undefined routes render the 404 component (not a redirect,
    // preserves the URL so the user sees what they typed).
    path: '**',
    component: NotFoundComponent,
  },
];

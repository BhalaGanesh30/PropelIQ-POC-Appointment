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
    // US_001 Edge Case: undefined routes render the 404 component (not a redirect,
    // preserves the URL so the user sees what they typed).
    path: '**',
    component: NotFoundComponent,
  },
];
